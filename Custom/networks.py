"""
Custom Actor and Critic networks for ML-Agents PPO.
Keep your custom network code here, separate from ml-agents source.
"""

from typing import List, Dict, Any, Tuple, Optional, Union
import numpy as np

from mlagents.torch_utils import torch, nn

from mlagents_envs.base_env import ActionSpec, ObservationSpec
from mlagents.trainers.settings import NetworkSettings
from mlagents.trainers.torch_entities.agent_action import AgentAction
from mlagents.trainers.torch_entities.action_model import ActionModel
from mlagents.trainers.torch_entities.layers import LinearEncoder, LSTM
from mlagents.trainers.buffer import AgentBuffer

from .models import LidarCnnConfig, StateMlpConfig, SensorFusionConfig, SensorFusion

class CustomActor(nn.Module):
    """
    Custom Actor using SensorFusion (LidarCnn + StateMlp + Attention) for ppo

    observation_specs is a List - one ObservationSpec per sensor:
        observation_specs[0] -> first sensor (e.g., Lidar)
        observation_specs[1] -> second sensor (e.g., IMU)
        ...

    inputs during forward() matches this order:
        inputs[0] -> tensor from first sensor, shape (batch, *sensor_shape)
        inputs[1] -> tensor from second sensor
        ...
    """

    MODEL_EXPORT_VERSION = 3

    def __init__(
            self,
            observation_specs: List[ObservationSpec],
            network_settings: NetworkSettings,
            action_spec: ActionSpec,
            conditional_sigma: bool = False,
            tanh_squash: bool = False,
    ):
        super().__init__()
        self.observation_specs = observation_specs
        self.action_spec = action_spec

        # DEBUG INFO
        print("\n" + "=" * 60)
        print("CustomActor: Received observation specs")
        print("=" * 60)
        for i, spec in enumerate(observation_specs):
            print(f"  [{i}] {spec.name}")
            print(f"      Shape: {spec.shape}")
            print(f"      Type: {spec.observation_type}")
        print("=" * 60 + "\n")

        # Sort observations into lidar vs state
        self.lidar_indices = []
        self.state_indices = []
        self.lidar_size = 0
        self.state_size = 0

        for i, spec in enumerate(observation_specs):
            size = int(np.prod(spec.shape))
            if "LidarSensor" in spec.name:
                self.lidar_indices.append(i)
                self.lidar_size += size
            else:
                self.state_indices.append(i)
                self.state_size += size

        print(f"Lidar sensors: {self.lidar_indices} (total: {self.lidar_size})")
        print(f"State sensors: {self.state_indices} (total: {self.state_size})")

        # Build config
        # TODO: expose these via network_settings or yaml
        self.use_memory = network_settings.memory is not None
        if self.use_memory:
            self.context_length = network_settings.memory.sequence_length
            self.num_embeddings = network_settings.memory.memory_size
        else:
            raise RuntimeError("Cannot use custom attention without memory")

        lidar_config = LidarCnnConfig(
            in_channels=6,
            base_channels=32,
            num_levels=3,
        )
        state_config = StateMlpConfig(
            state_dim=self.state_size,
            hidden_dim=network_settings.hidden_units,
            num_layers=2,
        )

        fusion_config = SensorFusionConfig(
            lidar_config=lidar_config,
            state_config=state_config,
            num_embeddings=self.num_embeddings,
            num_head=4,
            block_size=self.context_length + 64,
            attention_drop=0.1,
            residual_drop=0.1,
        )

        self.sensor_fusion = SensorFusion(fusion_config)
        self.encoding_size = fusion_config.num_embeddings
        self._memory_size = self.context_length * self.num_embeddings

        # Action Model copied from Unity's implementation
        self.action_model = ActionModel(
            self.encoding_size,
            action_spec,
            conditional_sigma=conditional_sigma,
            tanh_squash=tanh_squash,
            deterministic=network_settings.deterministic,
        )

        # Export Parameters (for ONNX)
        self.version_number = nn.Parameter(torch.Tensor([self.MODEL_EXPORT_VERSION]), requires_grad=False)
        self.memory_size_vector = nn.Parameter(torch.Tensor([self._memory_size]), requires_grad=False)
        self.continuous_act_size_vector = nn.Parameter(torch.Tensor([int(action_spec.continuous_size)]),requires_grad=False)
        self.discrete_act_size_vector = nn.Parameter(torch.Tensor([action_spec.discrete_branches]), requires_grad=False)
        self.act_size_vector_deprecated = nn.Parameter(torch.Tensor([action_spec.continuous_size + sum(action_spec.discrete_branches)]), requires_grad=False)

    @property
    def memory_size(self) -> int:
        """Size of LSTM memory. 0 if not using LSTM."""
        return self._memory_size

    def update_normalization(self, buffer: AgentBuffer) -> None:
        """Called to update input normalization. Implement if using normalization."""
        pass

    def _memories_to_past_tokens(self, memories: Optional[torch.Tensor]) -> Optional[torch.Tensor]:
        """Unflatten memories -> (B, context_length, num_embeddings)"""
        if memories is None:
            return None
        return memories.squeeze(0).reshape(-1, self.context_length, self.num_embeddings)

    def _past_tokens_to_memories(self, past_tokens: torch.Tensor) -> torch.Tensor:
        """Flatten past_tokens -> (1, B, memory_size) to match LSTM format"""
        return past_tokens.reshape(-1, self._memory_size).unsqueeze(0)

    def _update_past_tokens(self, past_tokens: Optional[torch.Tensor], new_token: torch.Tensor) -> torch.Tensor:
        """Append new token, drop oldest if at capacity."""
        # new_token: (B, embed)
        new_token = new_token.unsqueeze(1)  # (B, 1, embed)

        if past_tokens is None:  # pad with zeros
            B = new_token.size(0)
            past_tokens = torch.zeros(B, self.context_length - 1, self.num_embeddings, device=new_token.device)
            return torch.cat([past_tokens, new_token], dim=1)
        else:  # drop old, append new
            return torch.cat([past_tokens[:, 1:, :], new_token], dim=1)

    def _encode_observations(
            self,
            inputs: List[torch.Tensor],
            memories: Optional[torch.Tensor] = None,
            sequence_length: int = 1,
    ) -> Tuple[torch.Tensor, torch.Tensor]:
        # print(f"Actor: sequence_length: {sequence_length}, memories: {memories.shape if memories is not None else None}")
        # Gather lidar -> (B, 6, R)
        lidar_obs = [inputs[i] for i in self.lidar_indices]
        lidar_x = torch.cat(lidar_obs, dim=1).squeeze(-1)  # has to be (B, C, R)

        # Gather state -> (B, state_size)
        state_obs = [inputs[i].flatten(start_dim=1) for i in self.state_indices]
        state_x = torch.cat(state_obs, dim=1)

        # Unflatten and unroll memories
        actual_batch = lidar_x.size(0) // sequence_length
        lidar_x = lidar_x.reshape(actual_batch, sequence_length, *lidar_x.shape[1:])
        state_x = state_x.reshape(actual_batch, sequence_length, -1)

        past_tokens = self._memories_to_past_tokens(memories)
        encodings = []

        for t in range(sequence_length):
            enc = self.sensor_fusion(
                lidar_x[:, t],  # (actual_batch, 6, R)
                state_x[:, t],  # (actual_batch, state_dim)
                past_tokens
            )
            encodings.append(enc)
            past_tokens = self._update_past_tokens(past_tokens, enc)

        # Stack and flatten back to (B, embed)
        encoding = torch.stack(encodings, dim=1)  # (actual_batch, seq, embed)
        encoding = encoding.reshape(-1, self.num_embeddings)  # (B, embed)

        # Update past tokens
        memories_out = self._past_tokens_to_memories(past_tokens)

        return encoding, memories_out

    def get_action_and_stats(
        self,
        inputs: List[torch.Tensor],
        masks: Optional[torch.Tensor] = None,
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[AgentAction, Dict[str, Any], torch.Tensor]:
        """
        INFERENCE: Called every step to get actions.
        """
        encoding, memories = self._encode_observations(inputs, memories, sequence_length)

        # Sample action from distribution
        action, log_probs, entropy = self.action_model(encoding, masks)

        run_out = {
            "env_action": action.to_action_tuple(clip=self.action_model.clip_action),
            "log_probs": log_probs,
            "entropy": entropy,
        }

        return action, run_out, memories

    def get_stats(
        self,
        inputs: List[torch.Tensor],
        actions: AgentAction,
        masks: Optional[torch.Tensor] = None,
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Dict[str, Any]:
        """
        TRAINING: Compute log_probs and entropy for actions already taken.
        """
        encoding, _ = self._encode_observations(inputs, memories, sequence_length)

        log_probs, entropy = self.action_model.evaluate(encoding, masks, actions)

        return {
            "log_probs": log_probs,
            "entropy": entropy,
        }

    def forward(
        self,
        inputs: List[torch.Tensor],
        masks: Optional[torch.Tensor] = None,
        memories: Optional[torch.Tensor] = None,
    ) -> Tuple[Union[int, torch.Tensor], ...]:
        """
        ONNX EXPORT: Required for exporting model to Unity.
        Don't modify the signature - Unity expects this format.
        """
        encoding, memories_out = self._encode_observations(inputs, memories, sequence_length=1)

        (cont_action_out, disc_action_out, action_out_deprecated, deterministic_cont_action_out, deterministic_disc_action_out,
            ) = self.action_model.get_action_out(encoding, masks)

        export_out = [self.version_number, self.memory_size_vector]
        if self.action_spec.continuous_size > 0:
            export_out += [
                cont_action_out,
                self.continuous_act_size_vector,
                deterministic_cont_action_out,
            ]
        if self.action_spec.discrete_size > 0:
            export_out += [
                disc_action_out,
                self.discrete_act_size_vector,
                deterministic_disc_action_out,
            ]
        if self._memory_size > 0:
            export_out += [memories_out]
        return tuple(export_out)

class CustomCritic(nn.Module):
    """
    Custom Critic (Value Network) for PPO.

    Estimates V(s) - the expected future reward from a state.
    """

    def __init__(
        self,
        stream_names: List[str],
        observation_specs: List[ObservationSpec],
        network_settings: NetworkSettings,
    ):
        super().__init__()

        # Sort observations into lidar vs state
        self.lidar_indices = []
        self.state_indices = []
        self.lidar_size = 0
        self.state_size = 0

        for i, spec in enumerate(observation_specs):
            size = int(np.prod(spec.shape))
            if "LidarSensor" in spec.name:
                self.lidar_indices.append(i)
                self.lidar_size += size
            else:
                self.state_indices.append(i)
                self.state_size += size

        # Memory settings
        self.use_memory = network_settings.memory is not None
        if self.use_memory:
            self.context_length = network_settings.memory.sequence_length
            self.num_embeddings = network_settings.memory.memory_size
        else:
            raise RuntimeError("Cannot use custom attention without memory")

        # Build config (same as actor)
        lidar_config = LidarCnnConfig(
            in_channels=6,
            base_channels=32,
            num_levels=3,
        )
        state_config = StateMlpConfig(
            state_dim=self.state_size,
            hidden_dim=network_settings.hidden_units,
            num_layers=2,
        )
        fusion_config = SensorFusionConfig(
            lidar_config=lidar_config,
            state_config=state_config,
            num_embeddings=self.num_embeddings,
            num_head=4,
            block_size=self.context_length + 64,
            attention_drop=0.1,
            residual_drop=0.1,
        )

        self.sensor_fusion = SensorFusion(fusion_config)
        self.encoding_size = self.num_embeddings
        self._memory_size = self.context_length * self.num_embeddings if self.use_memory else 0

        # Value heads - one per reward stream
        self.value_heads = nn.ModuleDict({
            name: nn.Linear(self.encoding_size, 1) for name in stream_names
        })

    @property
    def memory_size(self) -> int:
        return self._memory_size

    def _memories_to_past_tokens(self, memories: Optional[torch.Tensor]) -> Optional[torch.Tensor]:
        if memories is None:
            return None
        return memories.view(-1, self.context_length, self.num_embeddings)

    def _past_tokens_to_memories(self, past_tokens: torch.Tensor) -> torch.Tensor:
        return past_tokens.view(1, -1, self._memory_size)

    def _update_past_tokens(self, past_tokens: Optional[torch.Tensor], new_token: torch.Tensor) -> torch.Tensor:
        new_token = new_token.unsqueeze(1)

        if past_tokens is None:
            B = new_token.size(0)
            past_tokens = torch.zeros(B, self.context_length - 1, self.num_embeddings, device=new_token.device)
            return torch.cat([past_tokens, new_token], dim=1)
        else:
            return torch.cat([past_tokens[:, 1:, :], new_token], dim=1)

    def _encode_observations(
            self,
            inputs: List[torch.Tensor],
            memories: Optional[torch.Tensor] = None,
            sequence_length: int = 1,
    ) -> Tuple[torch.Tensor, torch.Tensor]:
        # print(f"Critic: sequence_length: {sequence_length}, memories: {memories.shape if memories is not None else None}")
        # Gather lidar -> (B, 6, R)
        lidar_obs = [inputs[i] for i in self.lidar_indices]
        lidar_x = torch.cat(lidar_obs, dim=1).squeeze(-1) # has to be (B, C, R)

        # Gather state -> (B, state_size)
        state_obs = [inputs[i].flatten(start_dim=1) for i in self.state_indices]
        state_x = torch.cat(state_obs, dim=1)

        # Unflatten and unroll memories
        actual_batch = lidar_x.size(0) // sequence_length
        lidar_x = lidar_x.reshape(actual_batch, sequence_length, *lidar_x.shape[1:])
        state_x = state_x.reshape(actual_batch, sequence_length, -1)

        past_tokens = self._memories_to_past_tokens(memories)
        encodings = []

        for t in range(sequence_length):
            enc = self.sensor_fusion(
                lidar_x[:, t],  # (actual_batch, 6, R)
                state_x[:, t],  # (actual_batch, state_dim)
                past_tokens
            )
            encodings.append(enc)
            past_tokens = self._update_past_tokens(past_tokens, enc)

        # Stack and flatten back to (B, embed)
        encoding = torch.stack(encodings, dim=1)  # (actual_batch, seq, embed)
        encoding = encoding.reshape(-1, self.num_embeddings)  # (B, embed)

        # Update past tokens
        memories_out = self._past_tokens_to_memories(past_tokens)

        return encoding, memories_out

    def critic_pass(
            self,
            inputs: List[torch.Tensor],
            memories: Optional[torch.Tensor] = None,
            sequence_length: int = 1,
    ) -> Tuple[Dict[str, torch.Tensor], torch.Tensor]:
        encoding, memories = self._encode_observations(inputs, memories, sequence_length)

        value_outputs = {
            name: head(encoding).squeeze(-1) for name, head in self.value_heads.items()
        }

        return value_outputs, memories

    def update_normalization(self, buffer: AgentBuffer) -> None:
        pass
