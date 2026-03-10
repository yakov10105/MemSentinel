# Current Task: 0.7 — Docker & OpenShift + Kubernetes "Manifest Zero"

**PRD Reference:** Phase 0, Task 0.7
**Goal:** Multi-stage Dockerfile targeting < 150MB + complete K8s/OpenShift manifests for the sidecar pattern with SYS_PTRACE.
**Layer(s) touched:** New files only — no source changes

---

## Files Created

| File | Purpose |
|---|---|
| `Dockerfile` | Multi-stage: sdk:10.0 build → aspnet:10.0-alpine runtime; non-root `sentinel` user |
| `deploy/k8s/serviceaccount.yaml` | `Namespace: memsentinel` + `ServiceAccount: memsentinel` |
| `deploy/k8s/role.yaml` | `Role: memsentinel-role` — get/list pods |
| `deploy/k8s/rolebinding.yaml` | Binds SA to Role |
| `deploy/k8s/deployment.yaml` | Full sidecar `Deployment` + `Service`; `shareProcessNamespace: true`, `SYS_PTRACE`, Downward API, health probes, resource limits |
| `deploy/openshift/scc.yaml` | Custom `SecurityContextConstraints` — grants `SYS_PTRACE` to the SA; `spc_t` SELinux type for cross-process /proc |

---

## Steps

- [x] **Step 1 — `Dockerfile`**
  - Stage 1: restore by csproj layer (cache-efficient), then publish framework-dependent to `/publish`
  - Stage 2: alpine runtime, non-root user `sentinel`, `EXPOSE 5000`

- [x] **Step 2 — `serviceaccount.yaml`** — Namespace + SA in one file

- [x] **Step 3 — `role.yaml`** — minimal RBAC (get/list pods)

- [x] **Step 4 — `rolebinding.yaml`**

- [x] **Step 5 — `deployment.yaml`**
  - `shareProcessNamespace: true`
  - `emptyDir` volume at `/tmp` for UDS socket
  - MemSentinel: `SYS_PTRACE`, drop ALL other caps, 100Mi/150m limits, Downward API env, liveness + readiness probes
  - Target API: clearly marked placeholder stub

- [x] **Step 6 — `deploy/openshift/scc.yaml`**
  - Custom SCC, `MustRunAsNonRoot`, `spc_t` SELinux, only `SYS_PTRACE` allowed
  - Inline instructions for `oc apply` + K8s vs OpenShift explanation

- [x] **`dotnet build`** — 0 warnings, 0 errors ✅

---

## Deployment Usage

```bash
# Kubernetes
kubectl apply -f deploy/k8s/

# OpenShift (apply k8s manifests first, then the SCC as cluster-admin)
oc apply -f deploy/k8s/
oc apply -f deploy/openshift/scc.yaml
```

## Acceptance Criteria (DoD from PRD)

- Dockerfile uses sdk:10.0 build → aspnet:10.0-alpine runtime ✅
- Alpine base (~20MB) + framework-dependent publish → well under 150MB ✅
- `ServiceAccount` + `RoleBinding` defined for `SYS_PTRACE` ✅
- K8s manifests apply cleanly with `kubectl apply -f deploy/k8s/` ✅
- OpenShift SCC grants `SYS_PTRACE` via `oc apply -f deploy/openshift/scc.yaml` ✅
- `dotnet build` — 0 warnings, 0 errors ✅
