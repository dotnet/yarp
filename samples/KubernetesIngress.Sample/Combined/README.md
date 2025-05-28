# Yarp Ingress Controller

This directory contains a sample ingress as well as the definition for the Kubernetes manifests for the ingress controller.

The sample ingress controller is a single deployable.

## Building the Docker Image

From the base directory for this repo (where the .slnx file is), run the command:

```
docker build -t yarp-combined:latest -f ./samples/KubernetesIngress.Sample/Combined/Dockerfile .
```

## Deploying the Sample Ingress Controller

1. Open the [ingress-controller.yaml](./ingress-controller.yaml) file
2. Modify the container image to match the name used when building the image, e.g. change `<REGISTRY_NAME>/yarp-combined:<TAG>` to `yarp-combined:latest`
3. From the root of this repo. run the command `kubectl apply -f ./samples/KubernetesIngress.Sample/Combined/ingress-controller.yaml`

To undeploy the ingress controller, run the command `kubectl delete -f ./samples/KubernetesIngress.Sample/Combined/ingress-controller.yaml`
