/**
 * Opt out of browser history/credential filling without blocking typing, paste,
 * accessibility tools, or application-supplied values. Credential controls use
 * masked text inputs separately so Chrome does not classify them as passwords.
 */
export function browserAutofillAttributes(): Record<string, string> {
  return {
    autocomplete: "off",
    "data-lpignore": "true",
    "data-1p-ignore": "true",
    "data-bwignore": "true",
  };
}
