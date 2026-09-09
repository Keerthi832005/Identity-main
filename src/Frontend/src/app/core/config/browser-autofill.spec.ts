import { TestBed } from "@angular/core/testing";
import { AppInputComponent } from "../../shared/ui/form-controls/app-input.component";
import { AppSelectComponent } from "../../shared/ui/form-controls/app-select.component";
import { AppTextAreaComponent } from "../../shared/ui/form-controls/app-text-area.component";
import { browserAutofillAttributes } from "./browser-autofill";
import { configureEditorAutofill } from "./configure-editor-autofill";
import TextBox from "devextreme/ui/text_box";

describe("browser autofill policy", () => {
  beforeAll(() => configureEditorAutofill());

  it.each([
    "text",
    "email",
    "search",
    "tel",
    "number",
    "date",
    "datetime-local",
    "time",
    "password",
    "secret",
  ])(
    "applies the policy to the native %s input, preserving label and name",
    async (type) => {
      const fixture = TestBed.createComponent(AppInputComponent);
      fixture.componentRef.setInput("type", type);
      fixture.componentRef.setInput("name", "entry");
      fixture.componentRef.setInput("ariaLabel", "Entry");
      fixture.detectChanges();
      await fixture.whenStable();
      const control = fixture.nativeElement.querySelector(
        'input:not([type="hidden"])',
      ) as HTMLInputElement;
      expect(control).not.toBeNull();
      for (const [name, value] of Object.entries(browserAutofillAttributes())) {
        expect(control.getAttribute(name)).toBe(value);
      }
      expect(control.name).toBe("entry");
      if (type === "password" || type === "secret")
        expect(control.type).toBe("text");
      expect(control.getAttribute("aria-label")).toBe("Entry");
      expect(control.value).toBe("");
      expect(control.readOnly).toBe(false);
      fixture.destroy();
    },
  );

  it("preserves intentional field values and application suggestions", async () => {
    const fixture = TestBed.createComponent(AppInputComponent);
    fixture.componentRef.setInput("value", "WC-1");
    fixture.componentRef.setInput("suggestions", ["WC-1", "WC-2"]);
    fixture.detectChanges();
    await fixture.whenStable();
    const control = fixture.nativeElement.querySelector(
      'input:not([type="hidden"])',
    ) as HTMLInputElement;
    expect(control.value).toBe("WC-1");
    expect(control.getAttribute("autocomplete")).toBe("off");
    expect(
      fixture.nativeElement.querySelector("dx-autocomplete"),
    ).not.toBeNull();
    fixture.destroy();
  });

  it("suppresses autofill on textarea and select native editors", async () => {
    const area = TestBed.createComponent(AppTextAreaComponent);
    area.detectChanges();
    await area.whenStable();
    const select = TestBed.createComponent(AppSelectComponent);
    select.detectChanges();
    await select.whenStable();
    for (const control of [
      area.nativeElement.querySelector("textarea"),
      select.nativeElement.querySelector('input:not([type="hidden"])'),
    ] as HTMLElement[]) {
      for (const [name, value] of Object.entries(browserAutofillAttributes())) {
        expect(control.getAttribute(name)).toBe(value);
      }
    }
    area.destroy();
    select.destroy();
  });

  it("covers directly created grid/search editors while retaining their attributes", () => {
    const host = document.createElement("div");
    document.body.append(host);
    const options = { value: "", inputAttr: { "aria-label": "Grid filter" } };
    const editor = new TextBox(host, options);
    const control = host.querySelector('input:not([type="hidden"])')!;
    expect(control.getAttribute("aria-label")).toBe("Grid filter");
    for (const [name, value] of Object.entries(browserAutofillAttributes())) {
      expect(control.getAttribute(name)).toBe(value);
    }
    editor.dispose();
    host.remove();
  });
});
