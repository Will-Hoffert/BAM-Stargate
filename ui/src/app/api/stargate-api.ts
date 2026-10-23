import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';
import { RuntimeConfig } from '../config/runtime-config';
import { fromApiDate, toApiDate } from './api-dates';
import { toApiError } from './api-error';
import {
  AstronautDuty,
  NewDuty,
  Person,
  PersonDuties,
  ProcessLogEntry,
  ProcessLogLevel,
} from './api-models';

// The shapes the API sends. Each response also carries success, message and responseCode,
// which only matter when a call fails.

interface PersonResponse {
  personId: number;
  name: string;
  currentRank: string | null;
  currentDutyTitle: string | null;
  careerStartDate: string | null;
  careerEndDate: string | null;
}

interface DutyResponse {
  id: number;
  rank: string;
  dutyTitle: string;
  dutyStartDate: string;
  dutyEndDate: string | null;
}

interface ProcessLogEntryResponse {
  id: number;
  timestampUtc: string;
  level: ProcessLogLevel;
  operation: string;
  message: string;
  requestJson: string;
  durationMs: number;
}

interface IdResponse {
  id: number;
}

// The API takes a person's name as a bare JSON string, e.g. "John Doe". HttpClient would send
// a string as text/plain, which the API rejects, so these calls encode it and say it's JSON.
const jsonHeaders = { 'Content-Type': 'application/json' };

/** The Stargate API. Calls that fail throw an ApiError. */
@Injectable({ providedIn: 'root' })
export class StargateApi {
  private readonly http = inject(HttpClient);
  private readonly config = inject(RuntimeConfig);

  getPeople(): Observable<Person[]> {
    return this.call(this.http.get<{ people: PersonResponse[] }>(this.url('Person'))).pipe(
      map((response) => response.people.map(toPerson)),
    );
  }

  getPerson(name: string): Observable<Person> {
    return this.call(this.http.get<{ person: PersonResponse }>(this.url('Person', name))).pipe(
      map((response) => toPerson(response.person)),
    );
  }

  getDuties(name: string): Observable<PersonDuties> {
    return this.call(
      this.http.get<{ person: PersonResponse; astronautDuties: DutyResponse[] }>(
        this.url('AstronautDuty', name),
      ),
    ).pipe(
      map((response) => ({
        person: toPerson(response.person),
        duties: response.astronautDuties.map(toDuty),
      })),
    );
  }

  createPerson(name: string): Observable<number> {
    return this.call(
      this.http.post<IdResponse>(this.url('Person'), JSON.stringify(name), {
        headers: jsonHeaders,
      }),
    ).pipe(map((response) => response.id));
  }

  renamePerson(name: string, newName: string): Observable<number> {
    return this.call(
      this.http.put<IdResponse>(this.url('Person', name), JSON.stringify(newName), {
        headers: jsonHeaders,
      }),
    ).pipe(map((response) => response.id));
  }

  addDuty(duty: NewDuty): Observable<number> {
    const body = {
      name: duty.name,
      rank: duty.rank,
      dutyTitle: duty.dutyTitle,
      dutyStartDate: toApiDate(duty.dutyStartDate),
    };
    return this.call(this.http.post<IdResponse>(this.url('AstronautDuty'), body)).pipe(
      map((response) => response.id),
    );
  }

  /** The newest entries first. Without take, the API decides how many. */
  getProcessLog(take?: number): Observable<ProcessLogEntry[]> {
    const params: Record<string, number> = take === undefined ? {} : { take };
    return this.call(
      this.http.get<{ entries: ProcessLogEntryResponse[] }>(this.url('ProcessLog'), { params }),
    ).pipe(map((response) => response.entries.map(toProcessLogEntry)));
  }

  private url(resource: string, name?: string): string {
    const url = `${this.config.apiBaseUrl}/${resource}`;
    return name === undefined ? url : `${url}/${encodeURIComponent(name)}`;
  }

  private call<T>(request: Observable<T>): Observable<T> {
    return request.pipe(
      catchError((error: unknown) =>
        throwError(() => (error instanceof HttpErrorResponse ? toApiError(error) : error)),
      ),
    );
  }
}

function toPerson(person: PersonResponse): Person {
  return {
    ...person,
    careerStartDate: fromApiDate(person.careerStartDate),
    careerEndDate: fromApiDate(person.careerEndDate),
  };
}

function toDuty(duty: DutyResponse): AstronautDuty {
  return {
    ...duty,
    dutyStartDate: fromApiDate(duty.dutyStartDate),
    dutyEndDate: fromApiDate(duty.dutyEndDate),
  };
}

function toProcessLogEntry(entry: ProcessLogEntryResponse): ProcessLogEntry {
  return {
    id: entry.id,
    timestamp: new Date(entry.timestampUtc),
    level: entry.level,
    operation: entry.operation,
    message: entry.message,
    requestJson: entry.requestJson,
    durationMs: entry.durationMs,
  };
}
