import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    server: {
      // DevExtreme's extensionless directory imports require Vite resolution in jsdom.
      deps: { inline: [/devextreme/] },
    },
  },
});
