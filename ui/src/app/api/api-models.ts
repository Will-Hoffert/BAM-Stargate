/** A calendar date as yyyy-MM-dd, with no time or time zone. */
export type IsoDate = string;

export interface Person {
  personId: number;
  name: string;
  // Null until the person's first astronaut duty.
  currentRank: string | null;
  currentDutyTitle: string | null;
  careerStartDate: IsoDate | null;
  careerEndDate: IsoDate | null;
}

export interface AstronautDuty {
  id: number;
  rank: string;
  dutyTitle: string;
  dutyStartDate: IsoDate;
  dutyEndDate: IsoDate | null;
}

export interface PersonDuties {
  person: Person;
  /** Newest first. */
  duties: AstronautDuty[];
}

export interface NewDuty {
  name: string;
  rank: string;
  dutyTitle: string;
  /** Only the local calendar date is sent. */
  dutyStartDate: Date;
}

export type ProcessLogLevel = 'Information' | 'Warning' | 'Error';

export interface ProcessLogEntry {
  id: number;
  timestamp: Date;
  level: ProcessLogLevel;
  operation: string;
  message: string;
  requestJson: string;
  durationMs: number;
}
