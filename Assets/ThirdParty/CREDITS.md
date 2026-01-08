# Third-Party Credits

This document provides full attribution for all third-party assets and code used in this project.

---

## 3D Assets

### Katana Model

- **Title**: "katana"
- **Author**: Rubixboy101
- **Source**: https://skfb.ly/oov7S
- **License**: [Creative Commons Attribution 4.0 International (CC BY 4.0)](http://creativecommons.org/licenses/by/4.0/)
- **Usage**: Weapon model used in the project
- **Location**: `Assets/ThirdParty/katana.fbx`, `Assets/ThirdParty/KatanaEdit.*`

### Warehouse FBX Model

- **Title**: "Warehouse FBX Model Free"
- **Author**: Nicholas-3D
- **Source**: https://skfb.ly/oVOIy
- **License**: [Creative Commons Attribution 4.0 International (CC BY 4.0)](http://creativecommons.org/licenses/by/4.0/)
- **Usage**: Environment model used in the project

---

## Code Libraries

### ml-drone-collection

- **Author**: MBaske
- **Repository**: https://github.com/mbaske/ml-drone-collection
- **License**: [MIT License](https://github.com/mbaske/ml-drone-collection/blob/main/LICENSE.md) - See `Assets/ThirdParty/ml-drone-collection/LICENSE.md`
- **Copyright**: Copyright (c) MBaske
- **Usage**: Drone agent implementation and ML training utilities
- **Location**: `Assets/ThirdParty/ml-drone-collection/`

---

### ZigguratGaussian (Adapted from Redzen)

- **Original Author**: Colin D. Green
- **Original Repository**: https://github.com/colgreen/Redzen
- **License**: [MIT License](https://github.com/colgreen/Redzen/blob/master/LICENSE.txt)
- **Copyright**: Copyright (c) Colin D. Green
- **Modifications**: Adapted to work with UnityEngine.Random instead of System.Random
- **Usage**: High-performance Gaussian random number generation using the Ziggurat algorithm
- **Location**: `Assets/DodgingAgent/Scripts/Utilities/ZigguratGaussian.cs`

**Academic References**:
- Wikipedia: [Ziggurat algorithm](http://en.wikipedia.org/wiki/Ziggurat_algorithm)
- George Marsaglia and Wai Wan Tsang. ["The Ziggurat Method for Generating Random Variables"](http://www.jstatsoft.org/v05/i08/paper). Journal of Statistical Software, 2000.
- Jurgen A Doornik. ["An Improved Ziggurat Method to Generate Normal Random Samples"](http://www.doornik.com/research/ziggurat.pdf).

---

### IMU Noise Model (Kalibr)

- **Source**: ETH Zurich ASL - Kalibr
- **Documentation**: [IMU Noise Model](https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model)
- **Repository**: https://github.com/ethz-asl/kalibr
- **Usage**: Noise model implementation for IMU sensors (gyroscope and accelerometer) including white noise density and random walk parameters
- **Location**: `Assets/DodgingAgent/Scripts/Sensors/SensorImu.cs`

**Model Parameters**:
- Gyroscope white noise density (σ_g) and random walk (σ_bg)
- Accelerometer white noise density (σ_a) and random walk (σ_ba)

---

## License Compliance

All third-party assets and code used in this project are properly attributed according to their respective licenses. If you distribute this project or derivatives, please ensure you maintain these attributions and comply with the terms of each license.