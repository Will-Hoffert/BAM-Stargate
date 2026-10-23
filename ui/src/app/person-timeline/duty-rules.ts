// The API's rules for a duty's fields (DutyRules), checked here too so a mistake shows before
// anything is sent. The API still checks them.

export const maxDutyFieldLength = 100;

/** Why a rank or duty title would be rejected, or null if it's fine. Surrounding spaces don't count. */
export function dutyFieldError(value: string, fieldName: 'rank' | 'duty title'): string | null {
  const trimmed = value.trim();
  if (trimmed === '') return `A ${fieldName} is required.`;
  if (trimmed.length > maxDutyFieldLength) {
    return `A ${fieldName} can't be longer than ${maxDutyFieldLength} characters.`;
  }
  return null;
}

/** RETIRED ends a career. The API stores it in capitals but recognizes it in any case. */
export function isRetirement(dutyTitle: string): boolean {
  return dutyTitle.trim().toUpperCase() === 'RETIRED';
}
