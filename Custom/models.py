import torch
from torch import nn
import torch.nn.functional as F
import math


class ResidualBlock(nn.Module):
    def __init__(self, channels, use_skip=True, use_bn=True, act=nn.SELU, dropout=0.4, groups=1, d=1):
        super().__init__()
        conv_ = getattr(nn, f'Conv{d}d')
        batch_norm_ = getattr(nn, f'BatchNorm{d}d')

        self.dropout = dropout
        self.conv1 = conv_(channels, channels, 3, padding=1, bias=not use_bn, groups=groups)
        self.bn1 = batch_norm_(channels) if use_bn else nn.Identity()
        self.conv2 = conv_(channels, channels, 3, padding=1, bias=not use_bn, groups=groups)
        self.bn2 = batch_norm_(channels) if use_bn else nn.Identity()
        self.use_skip, self.act = use_skip, act

    def forward(self, x):
        if self.use_skip: x0 = x
        out = self.act()(self.bn1(self.conv1(x)))
        out = F.dropout(out, self.dropout, training=self.training)
        out = self.bn2(self.conv2(out))
        if self.use_skip: out = out + x0
        return self.act()(out)

class ResnetEncoder(nn.Module):
    def __init__(self, in_channels, latent_channels=1, base_channels=32, num_channels=3, blocks_per_level=4,
                    use_skips=True, use_bn=True, act=nn.SELU, dropout=0.4, groups=1, d=1):
        super().__init__()

        self.latent_shape = None # set first pass

        self._config = {
            "in_channels": in_channels,
            "latent_channels": latent_channels,
            "base_channels": base_channels,
            "num_channels": num_channels,
            "blocks_per_level": blocks_per_level,
            "use_skips": use_skips,
            "use_bn": use_bn,
            "act": act,
            "dropout": dropout,
            "groups": groups,
            "d": d,
        }
        conv_ = getattr(nn, f'Conv{d}d')
        batch_norm_ = getattr(nn, f'BatchNorm{d}d')
        self.avg_pool = getattr(F, f'avg_pool{d}d')

        self.conv1 = conv_(in_channels, base_channels, 3, padding=1, bias=not use_bn)  # No groups on first conv
        self.bn1 = batch_norm_(base_channels) if use_bn else nn.Identity()
        channels = [base_channels * 2 ** i for i in range(num_channels)]
        self.levels = nn.ModuleList(
            [nn.ModuleList([ResidualBlock(channel, use_skips, use_bn, act=act, dropout=dropout, groups=groups, d=d) for _ in range(blocks_per_level)]) for
                channel in channels])
        self.transitions = nn.ModuleList(
            [conv_(channels[i], channels[i + 1], 1, bias=not use_bn) for i in range(len(channels) - 1)])
        self.channel_proj = conv_(in_channels=channels[-1], out_channels=2 * latent_channels,
                                        kernel_size=1)  # 1x1 conv
        self.act = act

    def forward(self, x):
        if self.latent_shape is None:
            input_spatial = x.shape[2:]
            num_pools = self._config['num_channels'] - 1
            latent_spatial = tuple(s // (2 ** num_pools) for s in input_spatial)
            self.latent_shape = (self._config['latent_channels'],) + latent_spatial

        x = self.act()(self.bn1(self.conv1(x)))
        for i in range(len(self.levels)):
            if i > 0:  # shrink down
                x = self.avg_pool(x, 2)
                x = self.transitions[i - 1](x)
            for block in self.levels[i]:
                x = block(x)
        x = self.channel_proj(x)
        mean, logvar = x.chunk(2, dim=1)  # mean and log variance
        return mean, logvar

    @property
    def config(self):
        return self._config.copy()

class ResnetDecoder(nn.Module):
    def __init__(self, out_channels, latent_channels=1, base_channels=32, num_channels=3, blocks_per_level=4,
                    use_skips=True, use_bn=True, act=nn.SELU, dropout=0.4, groups=1, d=1):
        super().__init__()
        self._config = {
            "out_channels": out_channels,
            "latent_channels": latent_channels,
            "base_channels": base_channels,
            "num_channels": num_channels,
            "blocks_per_level": blocks_per_level,
            "use_skips": use_skips,
            "use_bn": use_bn,
            "act": act,
            "dropout": dropout,
            "groups": groups,
            "d": d,
        }

        conv_ = getattr(nn, f'Conv{d}d')
        self.interpolate_mode = {1: 'linear', 2: 'bilinear', 3: 'trilinear'}[d]

        channels = [base_channels * 2 ** i for i in range(num_channels)][::-1]
        self.channel_proj = conv_(in_channels=latent_channels, out_channels=channels[0], kernel_size=1)  # 1x1 conv
        self.levels = nn.ModuleList(
            [nn.ModuleList([ResidualBlock(channel, use_skips, use_bn, act=act, dropout=dropout, groups=groups, d=d) for _ in range(blocks_per_level)]) for channel in
                channels])
        self.transitions = nn.ModuleList(
            [conv_(channels[i], channels[i + 1], 1, bias=not use_bn) for i in range(len(channels) - 1)])
        self.final_conv = conv_(base_channels, out_channels, 3, padding=1)
        self.act = act

    def forward(self, z):
        x = self.channel_proj(z)
        for i in range(len(self.levels)):
            for block in self.levels[i]:
                x = block(x)
            if i < len(self.levels) - 1:  # not last level
                x = F.interpolate(x, scale_factor=2, mode=self.interpolate_mode, align_corners=False)
                x = self.transitions[i](x)
        return self.final_conv(x)

    @property
    def config(self):
        return self._config.copy()

class ResnetVAE(nn.Module):
    """Spatial VAE to try to encode lidar vectors into a 3 dimensional latent space"""

    def __init__(self, latent_channels=3, num_channels=3, act=nn.SELU, use_skips=True, use_bn=True, base_channels=32,
                    blocks_per_level=3, groups=1, dropout=0.4, d=1):
        super().__init__()

        self.channels = 1

        self.latent_channels = latent_channels
        self.num_channels = num_channels
        self.act = act
        self.use_skips = use_skips
        self.use_bn = use_bn
        self.base_channels = base_channels
        self.blocks_per_level = blocks_per_level
        self.groups = groups
        self.dropout = dropout
        self.d = d

        self.encoder = ResnetEncoder(
            in_channels=self.channels,
            latent_channels=self.latent_channels,
            num_channels=self.num_channels,
            base_channels=self.base_channels,
            blocks_per_level=self.blocks_per_level,
            use_skips=self.use_skips,
            use_bn=self.use_bn,
            act=self.act,
            dropout=self.dropout,
            groups=self.groups,
            d=self.d,
        )
        self.decoder = ResnetDecoder(
            out_channels=self.channels,
            latent_channels=self.latent_channels,
            num_channels=self.num_channels,
            base_channels=self.base_channels,
            blocks_per_level=self.blocks_per_level,
            use_skips=self.use_skips,
            use_bn=self.use_bn,
            act=self.act,
            dropout=self.dropout,
            groups=self.groups,
            d=self.d,
        )

    def forward(self, x):
        mu, log_var = self.encoder(x)
        z = torch.cat([mu, log_var], dim=1)
        z_hat = mu + torch.exp(0.5 * log_var) * torch.randn_like(mu)
        x_hat = self.decoder(z_hat)
        return z, x_hat, mu, log_var

    @property
    def config(self):
        """Return model configuration as dict for logging"""
        return {
            "latent_dim": self.latent_dim,
            "latent_shape": self.encoder.latent_shape if self.encoder.latent_shape else None,
            "act": self.act.__name__ if hasattr(self.act, '__name__') else str(self.act),
            "use_skips": self.use_skips,
            "use_bn": self.use_bn,
            "base_channels": self.base_channels,
            "blocks_per_level": self.blocks_per_level,
            "groups": self.groups,
            "dropout": self.dropout,
            "channels": self.channels,
            "model_class": self.__class__.__name__
        }

    @property
    def latent_dim(self):
        """Returns latent_dim after first forward pass, None before."""
        if self.encoder.latent_shape is None:
            return None
        return int(math.prod(self.encoder.latent_shape))

    def calculate_latent_dim(self, spatial_input):
        """Calculate what latent_dim would be for a given input size."""
        num_pools = self.num_channels - 1
        latent_spatial = tuple(s // (2 ** num_pools) for s in ((spatial_input,) if isinstance(spatial_input, int) else spatial_input))
        return self.latent_channels * math.prod(latent_spatial)

