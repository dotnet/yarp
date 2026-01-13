# Lets Encrypt Sample

[Lets Encrypt](https://letsencrypt.org/) is a certificate authority (CA) that provides HTTPS (SSL/TLS) certificates for free. This sample shows how to add Lets Encrypt for TLS termination in YARP by integrating with [LettuceEncrypt](https://github.com/natemcmaster/LettuceEncrypt). It allows to set up TLS between the client and YARP with minimal configuration.

The sample includes the following parts:

- **[Program.cs](Program.cs)**
  It calls `IServiceCollection.AddLettuceEncrypt` in the `ConfigureServices` method.

- **[appsettings.json](appsettings.json)**
  Sets up the required options for LettuceEncrypt including:
  - "DomainNames" - at least one domain name is required
  - "EmailAddress" - email address must be specified to register with the certificate authority
