export interface AppControlEventTarget {
  readonly value: string;
  readonly valueAsNumber: number;
  readonly checked: boolean;
}

export function createAppControlEvent(value: unknown, checked = false): Event {
  const text = value === null || value === undefined ? "" : String(value);
  const numeric = text.trim() ? Number(text) : Number.NaN;
  return {
    target: {
      value: text,
      valueAsNumber: Number.isFinite(numeric) ? numeric : Number.NaN,
      checked,
    } satisfies AppControlEventTarget,
  } as unknown as Event;
}
