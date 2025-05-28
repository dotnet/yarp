# Sample backend

This directory contains a sample ASP.NET Core application that acts as a "backend service" within a cluster as well as the definition for the Kubernetes manifests for the ingress controller.

## Building the Docker Images

From the base directory for this repo (where the .slnx file is), run the commands:

```
docker build -t backend:latest -f ./samples/KubernetesIngress.Sample/backend/Dockerfile .
```

## Deploying the Backend

1. Open the [backend.yaml](./backend.yaml) file
1. Modify the container image to match the name used when building the image, e.g. change `<REGISTRY_NAME>/backend:<TAG>` to `backend:latest`
1. Run the command `kubectl apply -f ./samples/KubernetesIngress.Sample/backend/backend.yaml`
1. Run the command `kubectl apply -f ./samples/KubernetesIngress.Sample/backend/ingress-sample.yaml`

To undeploy the backend, run the commands
```
kubectl delete -f ./samples/KubernetesIngress.Sample/backend/ingress-sample.yaml
kubectl delete -f ./samples/KubernetesIngress.Sample/backend/backend.yaml
```
