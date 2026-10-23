// The API's rules for a person's name (PersonRules.ValidateName), checked here too so a mistake
// shows before anything is sent. The API still checks them.

export const maxNameLength = 100;

/** Why a name would be rejected, or null if it's fine. Surrounding spaces don't count. */
export function nameError(name: string): string | null {
  const trimmed = name.trim();
  if (trimmed === '') return "A person's name is required.";
  if (trimmed.length > maxNameLength) {
    return `A person's name can't be longer than ${maxNameLength} characters.`;
  }
  return null;
}
