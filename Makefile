dashboard:
	uv run tensorboard --logdir results --port 6006 --bind_all

CONFIG = Assets/DodgingAgent/config/drone_beefy.yaml
NUM_ENVS ?= 128
NUM_AREAS ?= 32
ARGS ?=

PROJECT_ROOT := $(abspath $(dir $(lastword $(MAKEFILE_LIST))))

.PHONY: custom_train
custom_train:
	PYTHONPATH=$(PROJECT_ROOT) uv run mlagents-learn $(CONFIG) \
		--env=builds/$(MODEL).x86_64 \
		--run-id=$(RUN) \
		--num-envs=$(NUM_ENVS) \
		--num-areas=$(NUM_AREAS) \
		--no-graphics \
		$(ARGS)

.PHONY: train
train:
	uv run mlagents-learn $(CONFIG) \
		--env=builds/$(MODEL).x86_64 \
		--run-id=$(RUN) \
		--num-envs=$(NUM_ENVS) \
		--num-areas=$(NUM_AREAS) \
		--no-graphics \
		$(ARGS)
