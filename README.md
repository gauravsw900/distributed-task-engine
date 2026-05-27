# Distributed Task Engine

Async task processing system with priority queues, horizontal worker scaling, and a real-time monitoring dashboard.

Built with .NET 8, Redis, React, and Docker.

---

## How it works

Tasks are submitted via REST API and pushed into one of four Redis sorted sets (Critical / High / Normal / Low). Worker instances poll these queues in priority order, execute the task, and report results back. The React dashboard connects via SignalR for live updates.

Multiple workers can run simultaneously — each self-registers with a heartbeat and gets pruned automatically if it goes offline.

---

## Stack

- **API** — ASP.NET Core 8, SignalR
- **Workers** — .NET Worker Service
- **Queue** — Redis sorted sets (StackExchange.Redis)
- **Frontend** — React 18, Recharts
- **Infra** — Docker, Kubernetes + HPA

---

## Running locally

**With Docker (easiest):**
```bash
docker compose up --build
```

**Without Docker — need .NET 8 SDK, Node 20, Redis:**

Terminal 1:
```bash
cd src/TaskEngine.API && dotnet run
```

Terminal 2:
```bash
cd src/TaskEngine.Worker && dotnet run
```

Terminal 3:
```bash
cd frontend && npm install && npm start
```

Dashboard at http://localhost:3000, Swagger at http://localhost:5000/swagger.

Scale workers by opening more Terminal 2 instances.

---

## API

Submit a task:
```http
POST /api/tasks
{
  "type": "email",
  "priority": "High",
  "payload": { "to": "user@example.com", "subject": "Hello" },
  "maxRetries": 3
}
```

Other endpoints: `GET /api/tasks/{id}`, `DELETE /api/tasks/{id}`, `POST /api/tasks/{id}/retry`, `GET /api/tasks/stats`, `POST /api/tasks/bulk`

---

## Task types

`email`, `data-processing`, `report-generation`, `webhook`, `image-resize`

Adding a new type means implementing `ITaskHandler` and registering it in `Program.cs` — the worker picks it up automatically.

---

## Kubernetes

```bash
kubectl apply -f k8s/deployment.yaml
```

Includes an HPA that scales workers between 1-10 replicas based on CPU.

---

## TODO

- Add dead letter queue for tasks that fail all retries
- Prometheus metrics endpoint
- Auth on the API

## License
MIT
