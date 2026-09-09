import { CanDeactivateFn } from "@angular/router";
import type { OrganizationEditorComponent } from "./organization-editor.component";

export const canLeaveOrganizationEditor: CanDeactivateFn<
  OrganizationEditorComponent
> = (component) => component.canLeave();
