/**
 * Required-field feedback for the PTS editors.
 *
 * The editors carry the native `required` attribute, so the browser decides
 * when a field is missing on form submit. Its own bubble is suppressed and the
 * DevExtreme editor is put into its invalid state instead, so every form shows
 * one validation style without each page wiring up a validation group.
 */
export interface AppEditorInstance {
  option(name: string): unknown;
  option(name: string, value: unknown): void;
}

interface EditorValidationError {
  readonly editorSpecific?: boolean;
  readonly message?: string;
}

const GENERIC_REQUIRED_MESSAGE = "This field is required.";

type NativeControl = HTMLInputElement | HTMLTextAreaElement;

/** Replaces the browser's validation bubble with the editor's own message. */
export function wireNativeValidation(
  host: HTMLElement,
  wired: WeakSet<Element>,
  report: (control: NativeControl) => void,
): void {
  const controls = host.querySelectorAll<NativeControl>("input, textarea");
  controls.forEach((control) => {
    if (wired.has(control)) return;
    wired.add(control);
    control.addEventListener("invalid", (event) => {
      event.preventDefault();
      report(control);
    });
  });
}

/**
 * Says what is wrong in the application's own words. Every constraint the
 * editors can carry is covered; anything else falls back to the message the
 * browser would have shown, so no failure is reported without an explanation.
 */
export function constraintMessage(
  control: NativeControl,
  fieldName: string,
): string {
  const validity = control.validity;
  const name = fieldName || "This field";
  const limit = (attribute: string) => control.getAttribute(attribute) ?? "";

  if (validity.valueMissing) {
    return fieldName ? `${fieldName} is required.` : GENERIC_REQUIRED_MESSAGE;
  }
  if (validity.rangeUnderflow) {
    return `${name} must be ${limit("min")} or more.`;
  }
  if (validity.rangeOverflow) {
    return `${name} must be ${limit("max")} or less.`;
  }
  if (validity.stepMismatch) {
    return `${name} must match a step of ${limit("step")}.`;
  }
  if (validity.tooShort) {
    return `${name} must be at least ${limit("minlength")} characters.`;
  }
  if (validity.tooLong) {
    return `${name} must be ${limit("maxlength")} characters or fewer.`;
  }
  if (validity.typeMismatch) {
    return control.type === "email"
      ? `${name} must be a valid email address.`
      : `${name} is not in the expected format.`;
  }
  if (validity.patternMismatch) {
    return `${name} is not in the expected format.`;
  }
  if (validity.badInput) {
    return `${name} could not be read. Check the value.`;
  }
  return control.validationMessage || `${name} is not valid.`;
}

export function showValidationError(
  editor: AppEditorInstance | undefined,
  message: string,
): void {
  if (!editor) return;
  editor.option("validationErrors", [{ message }]);
  editor.option("validationError", { message });
  editor.option("isValid", false);
}

export function clearValidationError(
  editor: AppEditorInstance | undefined,
): void {
  if (!editor || editor.option("isValid") !== false) return;
  const error = editor.option("validationError") as
    EditorValidationError | undefined;
  if (error?.editorSpecific) return;
  editor.option("validationErrors", null);
  editor.option("validationError", null);
  editor.option("isValid", true);
}

/**
 * A DevExtreme number editor created without a value comes up carrying its own
 * "Value must be a number" error and re-applies it to every value that follows.
 * The bound value is always a number or null, so that error can only be stale.
 */
export function clearEditorParseError(
  editor: AppEditorInstance | undefined,
): void {
  if (!editor || editor.option("isValid") !== false) return;
  const error = editor.option("validationError") as
    EditorValidationError | undefined;
  if (!error?.editorSpecific) return;
  editor.option("validationErrors", null);
  editor.option("validationError", null);
  editor.option("isValid", true);
}

/** The field's name as the page labels it, for use inside a message. */
export function fieldName(host: HTMLElement, ariaLabel: string): string {
  const label =
    ariaLabel.trim() ||
    host.closest("label")?.querySelector("span")?.textContent?.trim() ||
    "";
  return label.replace(/\s*\*\s*$/, "").trim();
}
