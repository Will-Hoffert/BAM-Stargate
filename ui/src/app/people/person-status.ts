import { Person } from '../api/api-models';

export type PersonStatus = 'Active' | 'Retired' | 'Not an astronaut yet';

// The API sets a career end date only when someone retires, and a rank and duty title only
// once they have a duty.
export function personStatus(person: Person): PersonStatus {
  if (person.careerEndDate !== null) return 'Retired';
  if (person.currentDutyTitle !== null) return 'Active';
  return 'Not an astronaut yet';
}
