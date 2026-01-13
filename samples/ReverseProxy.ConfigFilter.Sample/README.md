# Configuration Filter Sample

This sample shows an example of a configuration filter. A configuration filter enables a callback as part of the configuration load where custom code can modify the configuration values for the proxy as they are loaded. This is valuable when the configuration file provides most of what you need, but you want to be able to tweak some values, but don't want to have to write a custom config provider.

## IProxyConfigFilter

The bulk of the code is the CustomConfigFilter class which implements the IProxyConfigFilter interface. The interface has two methods which act as callbacks when Clusters and Routes are loaded from config. The methods will be called for each Route and Cluster, and as both are defined as Records, they are immutable so the method should return the same object as-is or a replacement.

## CustomConfigFilter Class

### ConfigureClusterAsync
This looks at the value of each destination and sees whether it matches the pattern {{env_var_name}}, and if so it treats it as an indirection to an environment variable, and replaces the destination address with the value of the named variable (if it exists).

**Note:** AppSettings.json includes a destination of {{contoso}} which will be matched. The Properties/launchSettings.json file includes a definition of the environment variable, which will be used by Visual Studio and other tools when debugging with "F5".