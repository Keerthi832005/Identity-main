import { HttpErrorResponse, HttpHeaders } from "@angular/common/http";
import { DestroyRef } from "@angular/core";
import { TestBed } from "@angular/core/testing";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RetryCooldown } from "./retry-cooldown";

describe("RetryCooldown", () => {
  let cooldown: RetryCooldown;
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date("2026-08-30T12:00:00Z"));
    TestBed.configureTestingModule({});
    cooldown = new RetryCooldown(TestBed.inject(DestroyRef));
  });
  afterEach(() => {
    TestBed.resetTestingModule();
    vi.useRealTimers();
  });

  function throttled(value?: string): HttpErrorResponse {
    return new HttpErrorResponse({
      status: 429,
      headers:
        value === undefined
          ? new HttpHeaders()
          : new HttpHeaders({ "Retry-After": value }),
    });
  }

  it("counts down Retry-After seconds and stops without retrying any request", () => {
    expect(cooldown.capture(throttled("3"))).toBe(true);
    expect(cooldown.remainingSeconds()).toBe(3);
    vi.advanceTimersByTime(1000);
    expect(cooldown.remainingSeconds()).toBe(2);
    vi.advanceTimersByTime(2000);
    expect(cooldown.remainingSeconds()).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("accepts an HTTP-date Retry-After", () => {
    cooldown.capture(throttled("Sun, 30 Aug 2026 12:00:08 GMT"));
    expect(cooldown.remainingSeconds()).toBe(8);
  });

  it.each([
    undefined,
    "",
    "invalid",
    "-1",
    "1.5",
    "Infinity",
    "9999999999999999999999",
  ])(
    "uses a safe one-minute fallback for a missing or invalid header: %s",
    (value) => {
      cooldown.capture(throttled(value));
      expect(cooldown.remainingSeconds()).toBe(60);
    },
  );

  it.each(["0", "Sun, 30 Aug 2026 11:59:00 GMT"])(
    "waits at least one second for a zero or expired header: %s",
    (value) => {
      cooldown.capture(throttled(value));
      expect(cooldown.remainingSeconds()).toBe(1);
    },
  );

  it("does not treat invalid credentials or connection errors as throttling", () => {
    expect(cooldown.capture(new HttpErrorResponse({ status: 401 }))).toBe(
      false,
    );
    expect(cooldown.capture(new Error("network unavailable"))).toBe(false);
    expect(cooldown.remainingSeconds()).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("uses the deadline after a suspended/background tab resumes", () => {
    cooldown.capture(throttled("60"));
    vi.setSystemTime(new Date("2026-08-30T12:02:00Z"));
    vi.advanceTimersByTime(1000);
    expect(cooldown.remainingSeconds()).toBe(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it("replaces the previous timer and disposes it with the component", () => {
    cooldown.capture(throttled("20"));
    cooldown.capture(throttled("5"));
    expect(vi.getTimerCount()).toBe(1);
    expect(cooldown.remainingSeconds()).toBe(5);
    TestBed.resetTestingModule();
    expect(vi.getTimerCount()).toBe(0);
    expect(cooldown.capture(throttled("30"))).toBe(false);
    expect(vi.getTimerCount()).toBe(0);
  });
});
