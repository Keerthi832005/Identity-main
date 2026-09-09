export interface PastedCredential {
  readonly employeeCode: string;
  readonly password: string;
}

/** Password managers and spreadsheets label the pair; these are the labels seen in practice. */
const codeLabels =
  /^(employee\s*code|employee\s*id|emp\s*code|user(name)?|login|id)\s*[:=]\s*/i;
const passwordLabels = /^(password|pass|pwd|secret)\s*[:=]\s*/i;

/**
 * Reads an employee code and password out of one pasted blob.
 *
 * Accepts the shapes people actually paste: two spreadsheet cells, two lines, or a single labelled
 * or delimited pair. Returns null unless both halves are present, because a partial guess would put
 * a password into the employee-code field, where it is neither masked nor discarded.
 */
export function parseCredentialPaste(text: string): PastedCredential | null {
  if (!text) return null;

  const lines = text
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.length > 0);
  if (lines.length === 0 || lines.length > 4) return null;

  const labelled = fromLabelledLines(lines);
  if (labelled) return labelled;

  if (lines.length === 2) {
    return build(lines[0], lines[1]);
  }

  if (lines.length === 1) {
    /* Tab first: a spreadsheet pair is unambiguous, whereas a colon or comma can legitimately occur
       inside a password. */
    for (const separator of ["\t", "|"]) {
      const parts = lines[0].split(separator);
      if (parts.length === 2) return build(parts[0], parts[1]);
    }

    /* Split on the first occurrence only, so a password containing the separator survives intact. */
    for (const separator of [":", ","]) {
      const index = lines[0].indexOf(separator);
      if (index > 0) {
        return build(lines[0].slice(0, index), lines[0].slice(index + 1));
      }
    }
  }

  return null;
}

function fromLabelledLines(lines: readonly string[]): PastedCredential | null {
  let code: string | null = null;
  let password: string | null = null;

  for (const line of lines) {
    if (codeLabels.test(line)) code ??= line.replace(codeLabels, "").trim();
    else if (passwordLabels.test(line)) {
      password ??= line.replace(passwordLabels, "").trim();
    }
  }

  return code && password ? build(code, password) : null;
}

function build(code: string, password: string): PastedCredential | null {
  const employeeCode = code.trim();
  /* Only the surrounding whitespace a paste adds is removed; the password is otherwise untouched,
     since leading and trailing characters can be significant. */
  const secret = password.replace(/^\s+|\s+$/g, "");
  if (!employeeCode || !secret) return null;
  /* An employee code never contains whitespace; if it does, the split was wrong. */
  if (/\s/.test(employeeCode)) return null;
  return { employeeCode, password: secret };
}
