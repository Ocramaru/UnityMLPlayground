# Unity ML-Agents Training Project

A Unity project for training reinforcement learning agents using ML-Agents toolkit with custom network support.

## Setup

### Clone ML-Agents Ocramaru Fork

```bash
git clone https://github.com/Ocramaru/unity-ml-agents.git ml-agents
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

## Project Structure

```
.
├── Assets/
│   ├── DodgingAgent/             # Main project directory
│   │   ├── config/               # Training configurations
│   │   ├── Scripts/              # C# scripts
│   │   │   ├── Agents/           # ML-Agent implementations
│   │   │   ├── Core/             # Core game logic
│   │   │   ├── Sensors/          # Custom sensor implementations
│   │   │   ├── Weapons/          # Weapon systems
│   │   │   ├── Utilities/        # Helper utilities
│   │   │   └── Editor/           # Unity Editor extensions
│   │   ├── Prefabs/              # Unity prefabs
│   │   ├── Models/               # Trained ONNX models
│   │   ├── Resources/            # Unity resources
│   │   └── results/              # Training results and checkpoints
│   └── ThirdParty/               # Third-party assets
│
├── Custom/                       # Custom Python networks
│   ├── networks.py               # CustomActor, CustomActorCritic, CustomCritic
│   └── models.py                 # Model components (vae, cnn, etc.)
│
├── ml-agents/                    # ML-Agents toolkit fork
└── builds/                       # Unity executable builds for training
```

## Makefile Commands

### Training with executable builds

```bash
make train MODEL=<build_name> RUN=<run_id>
```

Optional parameters:
- `NUM_ENVS` - Number of parallel environments (default: 64)
- `NUM_AREAS` - Number of areas per environment (default: 16)
- `ARGS` - Additional mlagents-learn arguments

Example:
```bash
make train MODEL=linux_drone RUN=experiment_1 NUM_ENVS=32
```

### Training with custom networks

```bash
make custom_train MODEL=<build_name> RUN=<run_id>
```

This sets `PYTHONPATH` to include `Custom/` for custom network injection.

### TensorBoard Dashboard

```bash
make dashboard
```

Opens TensorBoard at http://localhost:6006

## Features

- Custom drone agent
- Bezier curve movement system
- Weapon orchestration system

## Credits

This project uses third-party assets and code:

### Assets
- **Katana Model**: ["katana"](https://skfb.ly/oov7S) by Rubixboy101 - CC BY 4.0
- **Warehouse FBX Model**: ["Warehouse FBX Model Free"](https://skfb.ly/oVOIy) by Nicholas-3D - CC BY 4.0

### Code Libraries
- **ml-drone-collection**: by MBaske - MIT License
- **ZigguratGaussian**: Adapted from [Redzen](https://github.com/colgreen/Redzen) by Colin D. Green - MIT License
- **IMU Noise Model**: Based on [Kalibr](https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model) by ETH Zurich ASL

See [Assets/ThirdParty/CREDITS.md](Assets/ThirdParty/CREDITS.md) for full attribution details.