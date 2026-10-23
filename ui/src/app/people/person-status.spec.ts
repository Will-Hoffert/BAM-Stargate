import { Person } from '../api/api-models';
import { personStatus } from './person-status';

const person: Person = {
  personId: 1,
  name: 'John Doe',
  currentRank: null,
  currentDutyTitle: null,
  careerStartDate: null,
  careerEndDate: null,
};

describe('personStatus', () => {
  it('should call someone with no duties not an astronaut yet', () => {
    expect(personStatus(person)).toBe('Not an astronaut yet');
  });

  it('should call someone whose career has not ended active', () => {
    expect(
      personStatus({
        ...person,
        currentRank: '1LT',
        currentDutyTitle: 'Commander',
        careerStartDate: '2020-01-01',
      }),
    ).toBe('Active');
  });

  it('should call someone whose career has ended retired', () => {
    expect(
      personStatus({
        ...person,
        currentRank: '1LT',
        currentDutyTitle: 'RETIRED',
        careerStartDate: '2020-01-01',
        careerEndDate: '2024-12-31',
      }),
    ).toBe('Retired');
  });
});
