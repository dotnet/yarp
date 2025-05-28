# Yarp Ingress Controller

This directory contains a sample ingress as well as the definition for the Kubernetes manifests for the ingress controller.

This sample requires two applications to be deployed:
* An Ingress (this application)
* A Kubernetes Ingress Monitor (a process listening for changes in k8s and dispatching the Yarp configuration to ingress instances)

NOTE: Yarp Kubernetes can also be configured as a combined (single) deployable. See the combined [README.md](../Combined/README.md) for more information.

## Building the Docker Images

From the base directory for this repo (where the .slnx file is), run the commands:

```
docker build -t yarp-monitor:latest -f ./samples/KubernetesIngress.Sample/Monitor/Dockerfile .
docker build -t yarp-ingress:latest -f ./samples/KubernetesIngress.Sample/Ingress/Dockerfile .
```

## Deploying the Sample Ingress Controller

1. Open the [ingress-monitor.yaml](../Monitor/ingress-monitor.yaml) file
1. Modify the container image to match the name used when building the image, e.g. change `<REGISTRY_NAME>/yarp-monitor:<TAG>` to `yarp-monitor:latest`
1. Run the command `kubectl apply -f ./samples/KubernetesIngress.Sample/Monitor/ingress-monitor.yaml`
1. Open the [ingress.yaml](./ingress.yaml) file
1. Modify the container image to match the name used when building the image, e.g. change `<REGISTRY_NAME>/yarp-ingress:<TAG>` to `yarp-ingress:latest`
1. Run the command `kubectl apply -f ./samples/KubernetesIngress.Sample/Ingress/ingress.yaml`

To undeploy the ingress, run the commands
```
kubectl delete -f ./samples/KubernetesIngress.Sample/Ingress/ingress.yaml
kubectl delete -f ./samples/KubernetesIngress.Sample/Monitor/ingress-monitor.yaml
```
