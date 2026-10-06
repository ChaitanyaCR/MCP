# IncidentPolisher.Web

Minimal incident testing interface with an incident note, audience selector,
example button, and structured results. The web host forwards requests to
`IncidentPolisher.Api`, so the browser needs no CORS configuration.

From the `IncidentPolisher/` directory, run these in separate terminals:

```sh
dotnet run --project src/IncidentPolisher.Api --no-launch-profile --urls http://localhost:5080
```

```sh
dotnet run --project src/IncidentPolisher.Web --no-launch-profile --urls http://localhost:5081
```

Open http://localhost:5081, choose **Load example**, then **Polish incident**.
The API defaults to fake data. Set its `AI:Provider` to `Ollama` and restart
the API to use your running LLM.

If the API runs elsewhere, set `IncidentApi:BaseUrl` in the web project's
`appsettings.json`, or set the `IncidentApi__BaseUrl` environment variable.
