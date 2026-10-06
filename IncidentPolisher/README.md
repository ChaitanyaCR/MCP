# IncidentPolisher

.NET 8 minimal API exposing `POST /api/incidents/polish`.

## Solution layout

```text
IncidentPolisher/
├── IncidentPolisher.sln
├── src/
│   ├── IncidentPolisher.Api/
│   │   ├── Contracts/
│   │   ├── Prompts/
│   │   ├── ChatClientFactory.cs
│   │   └── Program.cs
│   ├── IncidentPolisher.Application/
│   │   └── Incidents/
│   │       ├── IIncidentPolisher.cs
│   │       ├── IncidentPolisher.cs
│   │       ├── IncidentAudience.cs
│   │       ├── PolishedIncident.cs
│   │       ├── InvalidIncidentResponseException.cs
│   │       └── PromptBuilder.cs
│   └── IncidentPolisher.Web/
├── tests/
│   └── IncidentPolisher.Tests/
└── README.md
```

The API references Application. Application depends only on the provider-neutral
AI abstractions and has no dependency on ASP.NET Core, Ollama, or OpenAI SDKs.
The API supplies the prompt directory and selects the `IChatClient` adapter.
Web is a separate ASP.NET Core scaffold for a future UI.

For VS Code/C# Dev Kit, workspace settings select `IncidentPolisher.sln` when
opening either the repository root or the `IncidentPolisher/` folder. After
changing the solution layout, run **Developer: Reload Window** so the editor
loads the current solution rather than its previously generated cached solution.

From the `IncidentPolisher/` directory:

```sh
dotnet build IncidentPolisher.sln
dotnet test IncidentPolisher.sln
```

Regression tests cover the endpoint, validation, audience prompts, prompt-file
reloads, cancellation, structured model response validation, and provider configuration. Tests
use a fake chat client and do not require Ollama or Azure credentials.

The endpoint resolves `IIncidentPolisher`, implemented by `IncidentPolisher`, which calls
`Microsoft.Extensions.AI.IChatClient` through the configured provider. Request cancellation is
passed through to the model call.

The API defaults to `AI:Provider = Fake`. You can run the API and web UI without
an LLM or API credentials. Fake mode returns a fixed, clearly labeled demo
response for every valid incident note and audience; it does not analyze the note.
Input validation, prompt loading, and response validation still run normally.

When your LLM is ready, change `AI:Provider` in
`src/IncidentPolisher.Api/appsettings.json` from `Fake` to `Ollama` and restart the API.
You can also override the key at launch:

```sh
AI__Provider=Ollama dotnet run --project src/IncidentPolisher.Api --no-launch-profile --urls http://localhost:5080
```

For Ollama, start the server and download the configured model:

```sh
ollama pull llama3.2
ollama serve
```

If Ollama is already running, skip `ollama serve`. Configure `AI:Ollama:Endpoint` and
`AI:Ollama:Model` in `src/IncidentPolisher.Api/appsettings.json`, or override with `AI__Ollama__Endpoint` and
`AI__Ollama__Model` environment variables.

Run the API from the `IncidentPolisher/` directory:

```sh
dotnet run --project src/IncidentPolisher.Api --no-launch-profile --urls http://localhost:5080
```

Example request:

```sh
curl http://localhost:5080/api/incidents/polish \
  -H 'Content-Type: application/json' \
  -d '{"incidentNote":"Temporary degradation observed in payment service. Mitigation applied; monitoring now. Exact customer impact is unknown. Root cause has not been provided.","audience":"Leadership"}'
```

Example response (actual wording depends on the model):

```json
{
  "summary": "Temporary degradation observed in payment service.",
  "status": "Monitoring",
  "update": "The payment service experienced temporary degradation. Mitigation has been applied, and the service is being monitored. Exact customer impact remains unknown, and a root cause has not been provided.",
  "missingInformation": [
    "Exact customer impact is unknown",
    "Root cause has not been provided"
  ]
}
```

Audience accepts `Engineering`, `Leadership`, or `Customer` (or their numeric enum values). Empty notes and undefined audience values return HTTP 400.

`IIncidentPolisher.PolishAsync` returns a typed `PolishedIncident`, mapped to
`PolishIncidentResponse` by the API. The response now has `summary`, `status`,
`update`, and `missingInformation`; this replaces the previous `polishedUpdate`
field. All four fields are required. The first three must be non-empty strings,
and `missingInformation` must be an array of non-empty strings (or `[]`). Status
is descriptive text, with `Unknown` requested when the note does not establish
the current state. Missing information describes gaps without guessing facts.

The model is requested to use JSON mode through
[`ChatOptions.ResponseFormat`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.chatoptions.responseformat).
Use a provider/model that supports JSON output. Returned JSON is deserialized
and validated locally; malformed JSON, missing/extra fields, nulls, or invalid
field types return HTTP 502 with a generic Problem Details response. Validation
checks the response structure; factuality remains governed by the prompt rules.

The prompt adapts the update for the selected audience and instructs the model to
preserve facts and uncertainty. The selected provider must be reachable and its
configured model/deployment available for polishing requests to succeed.

## Switching AI providers

Both providers are included in the application. Set `AI:Provider` to `Ollama` or
`AzureFoundry` and restart the API. No code changes or rebuild are required.
Settings are validated at startup for the selected provider only.

For Azure Foundry, set these environment variables in your deployment:

```sh
export AI__Provider=AzureFoundry
export AI__AzureFoundry__Endpoint=https://YOUR-RESOURCE.openai.azure.com/openai/v1/
export AI__AzureFoundry__Deployment=YOUR-DEPLOYMENT-NAME
# Supply AI__AzureFoundry__ApiKey through your secret manager/environment.
```

Use the deployment name from Foundry, not the underlying model's name. This
adapter supports API-key authentication and Foundry's OpenAI v1 Chat Completions
endpoint. A project endpoint such as `/api/projects/...` is not a model endpoint.
The deployment must support Chat Completions. See Microsoft's
[Foundry v1 API documentation](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/api-version-lifecycle).
Keep API keys out of committed configuration files.

To switch back, set `AI__Provider=Ollama` and restart. `IncidentPolisher` and prompt
files remain unchanged. Additional provider protocols require an adapter to be
added once; these two supported providers can be switched through configuration.

## Editing LLM rules

Edit the Markdown files in `src/IncidentPolisher.Api/Prompts/`:

- `System.md`: shared role and rules for every audience.
- `Leadership.md`: leadership guidance.
- `Engineering.md`: engineering guidance.
- `Customer.md`: customer guidance.
- `ResponseFormat.md`: JSON output instructions and field guidance.

Each request reads `System.md`, the selected audience file, and `ResponseFormat.md` and combines them
into the system message. Edits apply to subsequent requests without rebuilding
or restarting the API. Keep files non-empty; missing or empty files fail the
request instead of sending a prompt without rules.

Prompt files are copied to build and publish output. For a deployed API, edit
the files in its content root's `Prompts/` directory. Run the published API from
the publish directory so that its content root points to those files.

```sh
dotnet publish src/IncidentPolisher.Api -o artifacts/api
cd artifacts/api
dotnet IncidentPolisher.Api.dll --urls http://localhost:5080
```
