# Unity ML-Agents Training Project

A Unity project for training reinforcement learning agents using ML-Agents toolkit.

## Setup

### Clone ML-Agents Release 23

```bash
git clone --branch release_23 https://github.com/Unity-Technologies/ml-agents.git
```

### Install Dependencies with uv

Install uv:

```bash
curl -LsSf https://astral.sh/uv/install.sh | sh
```

Sync dependencies with GPU support (CUDA 12.8):

```bash
uv sync --extra cu128
```

Or sync with CPU-only PyTorch:

```bash
uv sync --extra cpu
```

This will install ML-Agents from the local `ml-agents` directory and all required dependencies.

## Project Structure

```
Assets/
├── DodgingAgent/                 # Main project directory
│   ├── config/                   # Training configurations
│   │   └── drone_beefy.yaml     # Beefy config for DroneAgent
│   ├── Scripts/                  # C# scripts
│   │   ├── Agents/              # ML-Agent implementations
│   │   ├── Core/                # Core game logic
│   │   ├── Weapons/             # Weapon systems
│   │   ├── Sensors/             # Custom sensor implementations
│   │   ├── Utilities/           # Helper utilities (Bezier, Gaussian sampling, etc.)
│   │   └── Editor/              # Unity Editor extensions
│   ├── Prefabs/                 # Unity prefabs
│   ├── Models/                  # 3D models
│   ├── Materials/               # Unity materials
│   ├── Resources/               # Unity resources
│   └── results/                 # Training results and checkpoints
└── ThirdParty/                   # Third-party assets and libraries
    ├── ml-drone-collection/     # Drone implementation by MBaske
    └── CREDITS.md               # Full attribution for third-party content

ml-agents/                        # ML-Agents toolkit (clone this)
```

## Training

Run training with a configuration file:

```bash
uv run mlagents-learn Assets/DodgingAgent/config/drone.yaml --run-id=drone_training
```

### Using Unity Executable Builds

For faster training, you can use pre-built Unity executables instead of the Unity Editor:

```bash
uv run mlagents-learn Assets/DodgingAgent/config/drone.yaml \
  --env=builds/linux_drone_test/linux_drone_test.x86_64 \
  --run-id=my_training_run
```

To run multiple parallel environments for faster training:

```bash
uv run mlagents-learn Assets/DodgingAgent/config/drone.yaml \
  --env=builds/linux_drone_test/linux_drone_test.x86_64 \
  --run-id=my_training_run \
  --num-envs=16
```

This will spawn 16 Unity instances in parallel, significantly speeding up training.

### Monitoring Training with TensorBoard

View training metrics in real-time:

```bash
uv run tensorboard --logdir results --port 6006
```

Then open http://localhost:6006 in your browser to see training progress, rewards, and losses.

## Features

- Custom drone agent
- Bezier curve movement system
- Weapon orchestration system

## Credits

This project uses third-party assets and code:

### Assets
- **Katana Model**: ["katana"](https://skfb.ly/oov7S) by Rubixboy101 - CC BY 4.0

### Code Libraries
- **ml-drone-collection**: by MBaske - MIT License
- **ZigguratGaussian**: Adapted from [Redzen](https://github.com/colgreen/Redzen) by Colin D. Green - MIT License
- **IMU Noise Model**: Based on [Kalibr](https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model) by ETH Zurich ASL

See [Assets/ThirdParty/CREDITS.md](Assets/ThirdParty/CREDITS.md) for full attribution details.

---

## Modifications

<details>
<summary>Click to expand modifications made to ML-Agents</summary>

### ONNX Opset Version

**File:** `ml-agents/ml-agents/mlagents/trainers/settings.py`

Changed `onnx_opset` from `9` to `14` for Unity 6 Sentis compatibility:

```python
class SerializationSettings:
    convert_to_onnx = True
    onnx_opset = 14  # Changed to support newer ONNX
```

**Reason:** Unity 6 uses Sentis inference engine which supports ONNX opset 7-15. PyTorch 2.9 exports to opset 18 by default, and automatic downconversion to opset 9 fails. Opset 14 is the highest version that both Unity 6 Sentis and the conversion process support reliably.

</details>