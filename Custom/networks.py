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

from .models import ResnetVAE

class CustomActor(nn.Module):
    """
    Custom Actor network for PPO.

    observation_specs is a List - one ObservationSpec per sensor:
        observation_specs[0] -> first sensor (e.g., Lidar)
        observation_specs[1] -> second sensor (e.g., IMU)
        ...

    inputs during forward() matches this order:
        inputs[0] -> tensor from first sensor, shape (batch, *sensor_shape)
        inputs[1] -> tensor from second sensor
        ...
    """

    MODEL_EXPORT_VERSION = 3  # Required for ONNX export

    def __init__(self, observation_specs: List[ObservationSpec], network_settings: NetworkSettings,
        action_spec: ActionSpec, conditional_sigma: bool = False, tanh_squash: bool = False,):
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

        # Sort through Observations
        self.encoder_info = {
            "vae": {
                "indices": [],
                "indices_size": [],
                "total_size": 0
            },
            "mlp": {
                "indices": [],
                "indices_size": [],
                "total_size": 0
            },
            "total_size": 0,
            "hidden_units": network_settings.hidden_units,
            "memory_size": network_settings.memory.memory_size if network_settings.memory is not None else 0,
            "use_lstm": network_settings.memory is not None,
        }

        for i, spec in enumerate(observation_specs):
            size = int(np.prod(spec.shape))

            vae_or_mlp = "vae" if "LidarSensor" in spec.name else "mlp"
            self.encoder_info[vae_or_mlp]["indices"].append(i)
            self.encoder_info[vae_or_mlp]["indices_size"].append(size)
            self.encoder_info[vae_or_mlp]["total_size"] += size
            self.encoder_info["total_size"] += size  # add to full total

        print(f"VAE sensors: {self.encoder_info['vae']['indices']} (total: {self.encoder_info['vae']['total_size']})")
        print(f"MLP sensors: {self.encoder_info['mlp']['indices']} (total: {self.encoder_info['mlp']['total_size']})")

        # VAE
        self.vae = None
        self.vae_projection = None
        if self.encoder_info['vae']['total_size'] > 0:
            # set options
            vae_latent_channels = 2
            vae_num_channels = 4
            vae_base_channels = 32

            self.vae = ResnetVAE(
                latent_channels=vae_latent_channels,
                num_channels=vae_num_channels,
                base_channels=vae_base_channels,
                blocks_per_level=2,
                dropout=0.1,
                use_bn=False,  # Disable BatchNorm for ONNX export compatibility
                d=1,  # 1D
            )

            # Calculate latent dim for projection layer
            vae_latent_dim = self.vae.calculate_latent_dim(self.encoder_info['vae']['total_size'])
            self.vae_projection = nn.Linear(vae_latent_dim, self.encoder_info["hidden_units"])
            print(f"VAE latent dim: {vae_latent_dim} -> projection to {self.encoder_info['hidden_units']}")

        # Build simple mlp for others | TODO: KaimingHeNormal see if I should do this
        if self.encoder_info['mlp']['total_size'] > 0:
            mlp_layers = [nn.Linear(self.encoder_info['mlp']['total_size'], self.encoder_info["hidden_units"]), nn.SiLU()]
            for _ in range(network_settings.num_layers - 1):
                mlp_layers.append(nn.Linear(self.encoder_info["hidden_units"], self.encoder_info["hidden_units"]))
                mlp_layers.append(nn.SiLU())
            self.mlp_encoder = nn.Sequential(*mlp_layers)
        else:
            self.mlp_encoder = None

        # Fusion Layer
        if self.vae and self.mlp_encoder:
            self.fusion = nn.Linear(self.encoder_info["hidden_units"] * 2, self.encoder_info["hidden_units"])
        else:
            self.fusion = None

        # LSTM
        if self.encoder_info["use_lstm"]:
            self.lstm = LSTM(self.encoder_info["hidden_units"], self.encoder_info["memory_size"])
            self.encoding_size = self.encoder_info["memory_size"] // 2
        else:
            self.lstm = None
            self.encoding_size = self.encoder_info["hidden_units"]

        # Action Model copied from Unity's implementation
        self.action_model = ActionModel(self.encoding_size, action_spec, conditional_sigma=conditional_sigma, tanh_squash=tanh_squash, deterministic=network_settings.deterministic,)

        # Export Parameters (for Onnx Model)
        self.version_number = nn.Parameter(torch.Tensor([self.MODEL_EXPORT_VERSION]), requires_grad=False)
        self.memory_size_vector = nn.Parameter(torch.Tensor([self.encoder_info["memory_size"]]), requires_grad=False)
        self.continuous_act_size_vector = nn.Parameter(torch.Tensor([int(action_spec.continuous_size)]), requires_grad=False)
        self.discrete_act_size_vector = nn.Parameter(torch.Tensor([action_spec.discrete_branches]), requires_grad=False)
        self.act_size_vector_deprecated = nn.Parameter(torch.Tensor([action_spec.continuous_size + sum(action_spec.discrete_branches)]),requires_grad=False,)

    @property
    def memory_size(self) -> int:
        """Size of LSTM memory. 0 if not using LSTM."""
        return self.lstm.memory_size if self.encoder_info["use_lstm"] else 0

    def update_normalization(self, buffer: AgentBuffer) -> None:
        """Called to update input normalization. Implement if using normalization."""
        pass

    def _encode_observations(
        self,
        inputs: List[torch.Tensor],
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[torch.Tensor, torch.Tensor]:
        """
        Args:
            inputs: List of tensors, one per sensor
                inputs[0] shape: (batch, *observation_specs[0].shape)
                inputs[1] shape: (batch, *observation_specs[1].shape)
                ...
            memories: LSTM hidden state if using memory
            sequence_length: For LSTM batching

        Returns:
            encoding: (batch, encoding_size) tensor
            memories: Updated memory state
        """
        encodings = []

        # VAE path for lidar
        if self.vae is not None:
            vae_indices = self.encoder_info['vae']['indices']
            vae_obs = [inputs[i].flatten(start_dim=1) for i in vae_indices]
            vae_combined = torch.cat(vae_obs, dim=1)
            # Reshape to (batch, 1, length) for Conv1d
            vae_input = vae_combined.unsqueeze(1)
            # Get VAE latent (mu only for inference)
            mu, _ = self.vae.encoder(vae_input)
            vae_flat = mu.flatten(start_dim=1)
            vae_encoding = self.vae_projection(vae_flat)
            encodings.append(vae_encoding)

        # MLP path for other sensors
        if self.mlp_encoder is not None:
            mlp_indices = self.encoder_info['mlp']['indices']
            mlp_obs = [inputs[i].flatten(start_dim=1) for i in mlp_indices]
            mlp_combined = torch.cat(mlp_obs, dim=1)
            mlp_encoding = self.mlp_encoder(mlp_combined)
            encodings.append(mlp_encoding)

        # Combine encodings
        if len(encodings) == 2:
            encoding = self.fusion(torch.cat(encodings, dim=1))
        else:
            encoding = encodings[0]

        # LSTM if enabled
        if self.encoder_info["use_lstm"]:
            encoding = encoding.reshape([-1, sequence_length, self.encoder_info["hidden_units"]])
            encoding, memories = self.lstm(encoding, memories)
            encoding = encoding.reshape([-1, self.encoder_info["memory_size"] // 2])

        return encoding, memories

    def get_action_and_stats(
        self,
        inputs: List[torch.Tensor],
        masks: Optional[torch.Tensor] = None,
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[AgentAction, Dict[str, Any], torch.Tensor]:
        """
        INFERENCE: Called every step to get actions.

        Args:
            inputs: List of observation tensors from Unity sensors
            masks: Action masks for discrete actions
            memories: LSTM state
            sequence_length: For LSTM

        Returns:
            action: The sampled action
            run_out: Dict with 'env_action', 'log_probs', 'entropy'
            memories: Updated memory state
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

        Args:
            inputs: Observation tensors
            actions: Actions that were taken (from replay buffer)
            masks: Action masks
            memories: LSTM state
            sequence_length: For LSTM

        Returns:
            Dict with 'log_probs' and 'entropy'
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

        (cont_action_out, disc_action_out, action_out_deprecated, deterministic_cont_action_out,deterministic_disc_action_out,
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
        if self.encoder_info["memory_size"] > 0:
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

        self.observation_specs = observation_specs
        self.h_size = network_settings.hidden_units
        self.use_lstm = network_settings.memory is not None
        self.m_size = (
            network_settings.memory.memory_size
            if network_settings.memory is not None
            else 0
        )

        # Calculate total observation size
        total_obs_size = sum(int(np.prod(spec.shape)) for spec in observation_specs)

        # Encoder (can be same or different from actor)
        self.encoder = nn.Sequential(
            nn.Linear(total_obs_size, self.h_size),
            nn.ReLU(),
            nn.Linear(self.h_size, self.h_size),
            nn.ReLU(),
        )

        # LSTM if enabled
        if self.use_lstm:
            self.lstm = LSTM(self.h_size, self.m_size)
            self.encoding_size = self.m_size // 2
        else:
            self.lstm = None
            self.encoding_size = self.h_size

        # Value heads - one per reward stream (usually just "extrinsic")
        self.value_heads = nn.ModuleDict({
            name: nn.Linear(self.encoding_size, 1) for name in stream_names
        })

    @property
    def memory_size(self) -> int:
        return self.lstm.memory_size if self.use_lstm else 0

    def _encode_observations(
        self,
        inputs: List[torch.Tensor],
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[torch.Tensor, torch.Tensor]:
        """Encode observations to hidden representation."""
        flat_obs = [obs.flatten(start_dim=1) for obs in inputs]
        combined = torch.cat(flat_obs, dim=1)

        encoding = self.encoder(combined)

        if self.use_lstm:
            encoding = encoding.reshape([-1, sequence_length, self.h_size])
            encoding, memories = self.lstm(encoding, memories)
            encoding = encoding.reshape([-1, self.m_size // 2])

        return encoding, memories

    def critic_pass(
        self,
        inputs: List[torch.Tensor],
        memories: Optional[torch.Tensor] = None,
        sequence_length: int = 1,
    ) -> Tuple[Dict[str, torch.Tensor], torch.Tensor]:
        """
        Get value estimates for each reward stream.

        Returns:
            value_outputs: Dict mapping stream name to value tensor
            memories: Updated memory state
        """
        encoding, memories = self._encode_observations(inputs, memories, sequence_length)

        value_outputs = {
            name: head(encoding) for name, head in self.value_heads.items()
        }

        return value_outputs, memories

    def update_normalization(self, buffer: AgentBuffer) -> None:
        """Called to update input normalization. Implement if using normalization."""
        pass
