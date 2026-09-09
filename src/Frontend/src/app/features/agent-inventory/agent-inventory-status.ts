import type { AgentMachine } from "./agent-inventory.service";

export function machineStatus(machine: AgentMachine, now = Date.now()): string {
  if (!machine.isActive) return "Revoked";
  if (!machine.lastReportAt) return "Awaiting first report";
  const stamp = /Z$|[+-]\d\d:\d\d$/.test(machine.lastReportAt)
    ? machine.lastReportAt
    : machine.lastReportAt + "Z";
  return now - Date.parse(stamp) < 60 * 60 * 1000
    ? "Reporting"
    : "Report overdue";
}
