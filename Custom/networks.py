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
from mlagents.trainers.torch_entities.networks import Actor, Critic
from mlagents.trainers.torch_entities.decoders import ValueHeads
from mlagents.trainers.trajectory import ObsUtil
from mlagents.trainers.buffer import AgentBuffer

from .models import LidarCnnConfig, StateMlpConfig, SensorFusionConfig, SensorFusion

class RunningNorm(nn.Module):
    def __init__(self, size: int, eps: float = 1e-5):
        super().__init__()
        self.eps = eps
        self.register_buffer("count", torch.tensor(0.0))
        self.register_buffer("mean", torch.zeros(size))
        self.register_buffer("var", torch.ones(size))

    @torch.no_grad()
    def update(self, x: torch.Tensor):
        # x: (N, D)
        x = x.detach()
        batch_mean = x.mean(dim=0)
        batch_var = x.var(dim=0, unbiased=False)
        batch_count = float(x.shape[0])

        if self.count.item() == 0:
            self.mean.copy_(batch_mean)
            self.var.copy_(batch_var + self.eps)
            self.count.fill_(batch_count)
            return

        delta = batch_mean - self.mean
        tot = self.count + batch_count

        new_mean = self.mean + delta * (batch_count / tot)
        m_a = self.var * self.count
        m_b = batch_var * batch_count
        M2 = m_a + m_b + delta * delta * (self.count * batch_count / tot)
        new_var = M2 / tot

        self.mean.copy_(new_mean)
        self.var.copy_(new_var + self.eps)
        self.count.copy_(tot)

    def forward(self, x: torch.Tensor) -> torch.Tensor:
        return (x - self.mean) / torch.sqrt(self.var + self.eps)

    def copy_from(self, other: "RunningNorm"):
        self.count.copy_(other.count)
        self.mean.copy_(other.mean)
        self.var.copy_(other.var)

class Encoder(nn.Module):
    def __init__(
            self,
            observation_specs: ObservationSpec,
            network_settings: NetworkSettings,
    ):
        assert network_settings.memory is not None, "SharedEncoder requires memory"
        super().__init__()
        self.observation_specs = observation_specs

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

        # State Normalizer
        self.normalize = network_settings.normalize
        self.state_norm = RunningNorm(self.state_size) if self.normalize else nn.Identity()
        if self.normalize:
            print(f"Using normalize on state sensors. Total {self.state_size}")

        # Build config
        # TODO: expose these via network_settings or yaml
        self.context_length = network_settings.memory.sequence_length
        self.num_embeddings = network_settings.memory.memory_size
        self._memory_size = self.context_length * self.num_embeddings

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
            block_size=self.context_length,
            attention_drop=0.1,
            residual_drop=0.1,
        )

        self.sensor_fusion = SensorFusion(fusion_config)

    @property
    def memory_size(self) -> int:
        return self._memory_size

    def _memories_to_past_tokens(self, memories):
        # memories: (batch, 1, memory_size) -> (batch, context_length, num_embeddings)
        return memories.reshape(-1, self.context_length, self.num_embeddings)

    def _past_tokens_to_memories(self, past_tokens):
        return past_tokens.reshape(-1, self._memory_size).unsqueeze(0)

    def update_normalization(self, buffer: AgentBuffer) -> None:
        if not self.normalize: return
        obs = ObsUtil.from_buffer(buffer, len(self.observation_specs))

        parts = []
        for i in self.state_indices:
            x = torch.as_tensor(obs[i].to_ndarray(), dtype=torch.float32).flatten(start_dim=1)
            parts.append(x)

        state_x = torch.cat(parts, dim=1)  # (N, state_size)
        self.state_norm.update(state_x)

    def copy_normalization(self, other: "Encoder") -> None:
        if isinstance(self.state_norm, RunningNorm) and isinstance(other.state_norm, RunningNorm):
            self.state_norm.copy_from(other.state_norm)

    def encode(
            self,
            inputs: List[torch.Tensor],
            memories: torch.Tensor,
            sequence_length: int = 1,
    ) -> Tuple[torch.Tensor, torch.Tensor]:
        # Gather lidar -> (B, 6, R)
        lidar_obs = [inputs[i] for i in self.lidar_indices]
        lidar_x = torch.cat(lidar_obs, dim=1).squeeze(-1)  # has to be (B, C, R)

        # Gather state -> (B, state_size)
        state_obs = [inputs[i].flatten(start_dim=1) for i in self.state_indices]
        state_x = torch.cat(state_obs, dim=1)
        state_x = self.state_norm(state_x)  # identity if normalize = false

        # Unflatten and unroll memories
        lidar_x = lidar_x.reshape(lidar_x.size(0) // sequence_length, sequence_length, *lidar_x.shape[1:])
        state_x = state_x.reshape(state_x.size(0) // sequence_length, sequence_length, -1)

        past_tokens = self._memories_to_past_tokens(memories)
        encodings = []

        for t in range(sequence_length):
            enc = self.sensor_fusion(
                lidar_x[:, t],  # (B, 6, rays_dim)
                state_x[:, t],  # (B, state_dim)
                past_tokens
            )
            encodings.append(enc)
            past_tokens = torch.cat([past_tokens[:, 1:, :], enc.unsqueeze(1)], dim=1)

        # Stack and flatten back to (B, embed)
        encoding = torch.stack(encodings, dim=1)  # (actual_batch, seq, embed)
        encoding = encoding.reshape(-1, self.num_embeddings)  # (B, embed)

        # Update past tokens
        memories_out = self._past_tokens_to_memories(past_tokens)

        return encoding, memories_out

class CustomActor(nn.Module, Actor):
    """
    Custom Actor using SensorFusion (LidarCnn + StateMlp -> Attention -> Action) for ppo
    """

    MODEL_EXPORT_VERSION = 3

    def __init__(
            self,
            observation_specs: ObservationSpec,
            network_settings: NetworkSettings,
            action_spec: ActionSpec,
            conditional_sigma: bool = False,
            tanh_squash: bool = False,
    ):
        super().__init__()
        self.encoder = Encoder(observation_specs, network_settings)
        self.encoding_size = self.encoder.num_embeddings

        self.action_spec = action_spec
        self.action_model = ActionModel(
            self.encoding_size,
            action_spec,
            conditional_sigma=conditional_sigma,
            tanh_squash=tanh_squash,
            deterministic=network_settings.deterministic,
        )

        # Export Parameters (for ONNX)
        self.version_number = nn.Parameter(torch.Tensor([self.MODEL_EXPORT_VERSION]), requires_grad=False)
        self.memory_size_vector = nn.Parameter(torch.Tensor([self.encoder.memory_size]), requires_grad=False)
        self.continuous_act_size_vector = nn.Parameter(torch.Tensor([int(action_spec.continuous_size)]),requires_grad=False)
        self.discrete_act_size_vector = nn.Parameter(torch.Tensor([action_spec.discrete_branches]), requires_grad=False)
        self.act_size_vector_deprecated = nn.Parameter(torch.Tensor([action_spec.continuous_size + sum(action_spec.discrete_branches)]), requires_grad=False)

    @property
    def memory_size(self) -> int:
        return self.encoder.memory_size

    def update_normalization(self, buffer: AgentBuffer) -> None:
        self.encoder.update_normalization(buffer)

    def copy_normalization(self, other_network: "CustomActor") -> None:
        self.encoder.copy_normalization(other_network.encoder)

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
        encoding, memories = self.encoder.encode(inputs, memories, sequence_length)
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
        encoding, _ = self.encoder.encode(inputs, memories, sequence_length)

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
        encoding, memories_out = self.encoder.encode(inputs, memories, sequence_length=1)

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
        if self.encoder.memory_size > 0:
            export_out += [memories_out]
        return tuple(export_out)

class CustomCritic(nn.Module, Critic):
    """
    Custom Critic (Value Network) for PPO.

    Estimates V(s) - the expected future reward from a state.
    """

    def __init__(
        self,
        observation_specs: ObservationSpec,
        network_settings: NetworkSettings,
        stream_names: List[str],
    ):
        super().__init__()
        self.encoder = Encoder(observation_specs, network_settings)
        self.value_heads = ValueHeads(stream_names, self.encoding_size)

    @property
    def memory_size(self) -> int:
        return self.encoder.memory_size

    @property
    def encoding_size(self) -> int:
        return self.encoder.num_embeddings

    def critic_pass(
            self,
            inputs: List[torch.Tensor],
            memories: Optional[torch.Tensor] = None,
            sequence_length: int = 1,
    ) -> Tuple[Dict[str, torch.Tensor], torch.Tensor]:
        encoding, memories = self.encoder.encode(inputs, memories, sequence_length)

        value_outputs = {
            name: head(encoding).squeeze(-1) for name, head in self.value_heads.items()
        }

        return value_outputs, memories

    def update_normalization(self, buffer: AgentBuffer) -> None:
        self.encoder.update_normalization(buffer)

class CustomActorCritic(CustomActor, Critic):
    def __init__(
            self,
            observation_specs: List[ObservationSpec],
            network_settings: NetworkSettings,
            action_spec: ActionSpec,
            stream_names: List[str],
            conditional_sigma: bool = False,
            tanh_squash: bool = False,
    ):
        super().__init__(
            observation_specs,
            network_settings,
            action_spec,
            conditional_sigma,
            tanh_squash,
        )
        self.stream_names = stream_names
        self.value_heads = ValueHeads(stream_names, self.encoding_size)

    def critic_pass(
        self,
        inputs: List[torch.Tensor],
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[Dict[str, torch.Tensor], torch.Tensor]:
        encoding, memories_out = self.encoder.encode(inputs, memories, sequence_length)
        return self.value_heads(encoding), memories_out
