import { TestBed } from "@angular/core/testing";
import { NavigationStart, Router } from "@angular/router";
import { Subject } from "rxjs";
import { beforeEach, describe, expect, it } from "vitest";
import { ConfirmationService } from "./confirmation.service";

describe("Confirmation decisions", () => {
  let events: Subject<NavigationStart>;
  let service: ConfirmationService;
  const request = {
    title: "Revoke access?",
    message: "This access change is audited.",
    confirmText: "Revoke",
    tone: "danger" as const,
  };

  beforeEach(() => {
    events = new Subject<NavigationStart>();
    TestBed.configureTestingModule({
      providers: [{ provide: Router, useValue: { events } }],
    });
    service = TestBed.inject(ConfirmationService);
  });

  it("requires an explicit approval and clears the visible request", async () => {
    const result = service.ask(request);
    expect(service.active()).toEqual(request);
    service.respond(service.active()!, true);
    expect(await result).toBe(true);
    expect(service.active()).toBeNull();
  });

  it("treats dismissal as cancellation", async () => {
    const result = service.ask(request);
    service.cancel();
    expect(await result).toBe(false);
    expect(service.active()).toBeNull();
  });

  it("rejects duplicate requests without replacing the first decision", async () => {
    const first = service.ask(request);
    const active = service.active();
    expect(await service.ask({ ...request, title: "Another action" })).toBe(
      false,
    );
    expect(service.active()).toBe(active);
    service.respond(active!, true);
    expect(await first).toBe(true);
  });

  it("ignores late close events from an earlier dialog", async () => {
    const first = service.ask(request);
    const previous = service.active()!;
    service.respond(previous, true);
    expect(await first).toBe(true);
    const second = service.ask(request);
    const active = service.active()!;
    service.respond(previous, false);
    expect(service.active()).toBe(active);
    service.respond(active, true);
    expect(await second).toBe(true);
  });

  it("cancels an action on navigation but permits the following route guard to ask", async () => {
    const action = service.ask(request);
    events.next(new NavigationStart(1, "/applications"));
    expect(await action).toBe(false);
    const guard = service.ask({ ...request, title: "Discard changes?" });
    service.respond(service.active()!, true);
    expect(await guard).toBe(true);
  });

  it("cancels pending decisions on app teardown", async () => {
    const result = service.ask(request);
    TestBed.resetTestingModule();
    expect(await result).toBe(false);
  });
});
