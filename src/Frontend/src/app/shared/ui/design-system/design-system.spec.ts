import { Component, Type } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { describe, expect, it } from "vitest";
import { AppBusyOverlayComponent } from "./app-busy-overlay.component";
import { AppEmptyStateComponent } from "./app-empty-state.component";
import { AppErrorStateComponent } from "./app-error-state.component";
import { AppInlineAlertComponent } from "./app-inline-alert.component";
import { AppPageComponent } from "./app-page.component";
import { AppSkeletonComponent } from "./app-skeleton.component";

function render<T>(type: Type<T>): HTMLElement {
  const fixture = TestBed.createComponent(type);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

@Component({
  standalone: true,
  imports: [AppPageComponent],
  template: `<app-page title="Users" subtitle="Directory" eyebrow="Access" />`,
})
class PageHost {}

@Component({
  standalone: true,
  imports: [AppPageComponent],
  template: `<app-page title="Users"
    ><button page-actions type="button">Add</button></app-page
  >`,
})
class PageWithActionsHost {}

@Component({
  standalone: true,
  imports: [AppInlineAlertComponent],
  template: `<app-inline-alert tone="danger" heading="Rejected"
    >Row 3 failed.</app-inline-alert
  >`,
})
class DangerAlertHost {}

@Component({
  standalone: true,
  imports: [AppInlineAlertComponent],
  template: `<app-inline-alert tone="info">Staged.</app-inline-alert>`,
})
class InfoAlertHost {}

@Component({
  standalone: true,
  imports: [AppErrorStateComponent],
  template: `<app-error-state
    message="The identity service did not respond."
    correlationId="c4a1-7e02"
  />`,
})
class ErrorStateHost {}

@Component({
  standalone: true,
  imports: [AppEmptyStateComponent],
  template: `<app-empty-state title="No users match these filters" />`,
})
class EmptyStateHost {}

@Component({
  standalone: true,
  imports: [AppSkeletonComponent],
  template: `<app-skeleton [lines]="3" />`,
})
class SkeletonHost {}

@Component({
  standalone: true,
  imports: [AppBusyOverlayComponent],
  template: `<app-busy-overlay [busy]="true"
    ><button type="button">Save</button></app-busy-overlay
  >`,
})
class BusyHost {}

describe("Design system primitives", () => {
  it("renders the page scaffold title block", () => {
    const element = render(PageHost);
    expect(element.querySelector(".page__title")?.textContent).toBe("Users");
    expect(element.querySelector(".page__eyebrow")?.textContent).toBe("Access");
    expect(element.querySelector(".page__subtitle")?.textContent).toBe(
      "Directory",
    );
  });

  it("keeps the action slot available for projected content", () => {
    const element = render(PageWithActionsHost);
    expect(element.querySelector(".page__actions button")?.textContent).toBe(
      "Add",
    );
  });

  it("announces a danger alert assertively and a quiet tone politely", () => {
    const danger = render(DangerAlertHost).querySelector(".alert");
    expect(danger?.getAttribute("role")).toBe("alert");
    expect(danger?.getAttribute("aria-live")).toBe("assertive");

    const info = render(InfoAlertHost).querySelector(".alert");
    expect(info?.getAttribute("role")).toBe("status");
    expect(info?.getAttribute("aria-live")).toBe("polite");
  });

  it("always shows the correlation id on an error state", () => {
    const element = render(ErrorStateHost);
    expect(element.querySelector(".state__reference")?.textContent).toContain(
      "c4a1-7e02",
    );
    expect(element.querySelector(".state")?.getAttribute("role")).toBe("alert");
  });

  it("omits the message paragraph when an empty state has none", () => {
    const element = render(EmptyStateHost);
    expect(element.querySelector(".state__message")).toBeNull();
    expect(element.querySelector(".state")?.getAttribute("role")).toBe(
      "status",
    );
  });

  it("renders one skeleton bar per requested line", () => {
    const element = render(SkeletonHost);
    expect(element.querySelectorAll(".skeleton__bar").length).toBe(3);
  });

  it("makes covered content inert while busy", () => {
    const element = render(BusyHost);
    expect(element.querySelector(".busy__content")?.hasAttribute("inert")).toBe(
      true,
    );
    expect(element.querySelector(".busy__veil")).not.toBeNull();
  });
});
