import { bootstrapApplication } from "@angular/platform-browser";
import devExtremeConfig from "devextreme/core/config";
import { App } from "./app/app";
import { appConfig } from "./app/app.config";
import {
  RUNTIME_CONFIG,
  loadRuntimeConfig,
} from "./app/core/config/runtime-config";
import { licenseKey } from "./devextreme-license";
import { configureEditorAutofill } from "./app/core/config/configure-editor-autofill";

devExtremeConfig({ licenseKey });
configureEditorAutofill();

loadRuntimeConfig()
  .then((runtimeConfig) => {
    return bootstrapApplication(App, {
      ...appConfig,
      providers: [
        ...(appConfig.providers ?? []),
        { provide: RUNTIME_CONFIG, useValue: runtimeConfig },
      ],
    });
  })
  .catch((error: unknown) =>
    console.error("Identity Administration bootstrap failed", error),
  );
