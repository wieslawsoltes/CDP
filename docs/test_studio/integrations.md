# Test Management Service Integrations

CDP Test Studio and Recorder support bi-directional integrations with five popular test management service providers: **TestMo**, **TestRail**, **Qase**, **Xray**, and **Zephyr Scale**. 

The integration allows testing teams and automated agents to:
- Export recorded visual scenarios directly as structured manual test cases.
- Publish execution run results with step-by-step telemetry, statuses, logs, duration metrics, and attachment reports (PDF/HTML).
- Programmatically control and trigger connections headlessly using standard Chrome DevTools Protocol (CDP) commands.

---

## Architectural Overview

The integration layer is split into modular C# projects to prevent dependency coupling, ensure linker-trimming safety, and maintain a lightweight core:

1. **`CDP.Integration.Core`**: Defines standard contracts (`ITestService`), common configuration data structures (`TestServiceConfig`), case/step result payloads (`TestRunResultData`), and a global service register (`TestServiceRegistry`).
2. **Service Adapters**: Six individual client adapter assemblies implement the unified `ITestService` interface:
   - `CDP.Integration.TestMo`
   - `CDP.Integration.TestRail`
   - `CDP.Integration.Qase`
   - `CDP.Integration.Xray`
   - `CDP.Integration.Zephyr`

---

## Service-Specific Integration Details

### 1. TestMo
- **Authentication**: Bearer token auth (`Authorization: Bearer <API_TOKEN>`).
- **REST API Path**: `/api/v1/projects/{projectId}`
- **Exporting Cases**: Posts title, description, tags, and structured `custom_steps` directly to the project.
- **Publishing Runs**: 
  - Creates a new test run under the specified Milestone and Configurations if provided.
  - Submits individual step execution comments, statuses (`passed`/`failed`), and duration metrics in a single results payload.
  - PDF/HTML attachments are uploaded post-publish via the `/api/v1/results/{resultId}/attachments` endpoint.

### 2. TestRail
- **Authentication**: Basic Authentication (`Authorization: Basic <base64(username:API_TOKEN)>`).
- **REST API Path**: Root relative prefixing: `index.php?/api/v2/` (prevents query string dropouts during base address URI merges).
- **Exporting Cases**: Adds a test case under the configured section/suite ID, translating steps into `custom_steps_separated`.
- **Publishing Runs**:
  - If a `PlanId` is configured, it registers a new run under the test plan (`add_plan_entry`). Otherwise, it creates a standalone test run (`add_run`).
  - Step results are mapped to TestRail's custom separated steps structure using the `custom_step_results` property array.
  - Attaches execution reports to the result using `add_attachment_to_result`.

### 3. Qase
- **Authentication**: Custom HTTP header token auth (`Token: <API_TOKEN>`).
- **REST API Path**: `/v1/`
- **Exporting Cases**: Posts to `/v1/case/{projectCode}` with positioned test steps.
- **Publishing Runs**:
  - Registers execution runs linked to Milestone IDs or Test Plan IDs.
  - Uploads PDF/HTML reports via the `/v1/attachment/{projectCode}` multi-part request.
  - Submits step execution results using the `/v1/result/{projectCode}/{runId}` endpoint.

### 4. Xray
- **Authentication**: Exchanges Client ID (`Username`) and Client Secret (`Token`) for a Bearer token via `/api/v2/authenticate`.
- **REST API Path**: `/api/v2/`
- **Exporting Cases**: Automatically imports manual test case step parameters to Jira project boards via `/api/v2/import/test`.
- **Publishing Runs**:
  - Submits results via Xray JSON execution import (`/api/v2/import/execution`).
  - Populates version fields (milestones), plans, and environment arrays (configurations), detailing step-by-step statuses.

### 5. Zephyr Scale
- **Authentication**: JWT/Bearer Token auth (`Authorization: Bearer <API_TOKEN>`).
- **REST API Path**: `/v1/`
- **Exporting Cases**: Posts title and objective parameters to `/v1/testcases`.
- **Publishing Runs**:
  - Cycle cycle run keys are matched or created under folders.
  - Posts execution results, environments, and step status properties via `/v1/testruns/{cycleKey}/testresults`.

---

## Headless CDP Facade Scripting

External testing engines, CI/CD runners, and agent bots can control integrations headlessly. The `IntegrationsFacade` class is injected as a global script evaluation target `Integrations` inside the CDP Jint engine (`RuntimeDomain`).

### Available Script Methods

- `Integrations.SetProvider(string providerName)`: Selects the provider. Valid values: `"TestMo"`, `"TestRail"`, `"Qase"`, `"Xray"`, `"Zephyr"`.
- `Integrations.Configure(string url, string username, string token, string projectId, string suiteId, string milestoneId, string planId, string configurationId)`: Set connection details.
- `Integrations.VerifyConnection()`: Returns `true` if authentication handshakes succeed.
- `Integrations.ExportTestCase(string title, string description)`: Exports recorded steps as a case and returns the target Case ID.
- `Integrations.PublishRunResult(string testCaseId, string testName, string status, double durationMs)`: Publishes the run result and returns the execution ID.

### Script Evaluation Example

Using a CDP client, execute a `Runtime.evaluate` command with this expression:

```javascript
Integrations.SetProvider("TestRail");
Integrations.Configure("https://company.testrail.io", "agent@company.com", "token123", "1", "12");
var connected = Integrations.VerifyConnection();
if (connected) {
    Integrations.ExportTestCase("Visual Auto-Test Run", "Recorded via CDP Inspector");
}
```

---

## UI Settings Usage

1. Open the **Test Studio** tab in the Inspector.
2. Click the **Integrations** toggle button (indicated by a chain-link icon) in the environment toolbar.
3. Select the desired provider and enter the credentials.
4. Click **Verify Connection** to validate connection handshakes.
5. Click **Export Test Case** to push the recorded scenario to the server.
6. Execution run results, logs, and report attachments will publish automatically to the active integration provider upon step playback completion.
