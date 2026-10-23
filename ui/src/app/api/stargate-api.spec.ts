import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { RuntimeConfig } from '../config/runtime-config';
import { ApiError } from './api-error';
import { StargateApi } from './stargate-api';

// Every API response carries these alongside its data.
const successful = { success: true, message: 'Successful', responseCode: 200 };

const johnDoeResponse = {
  personId: 1,
  name: 'John Doe',
  currentRank: '1LT',
  currentDutyTitle: 'Commander',
  careerStartDate: '2020-01-01T00:00:00',
  careerEndDate: null,
};

const johnDoe = {
  personId: 1,
  name: 'John Doe',
  currentRank: '1LT',
  currentDutyTitle: 'Commander',
  careerStartDate: '2020-01-01',
  careerEndDate: null,
};

describe('StargateApi', () => {
  let api: StargateApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: RuntimeConfig, useValue: { apiBaseUrl: 'http://api.test' } },
      ],
    });
    api = TestBed.inject(StargateApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should get everyone, including people with no duties yet', async () => {
    const people = firstValueFrom(api.getPeople());

    const request = http.expectOne('http://api.test/Person');
    expect(request.request.method).toBe('GET');
    request.flush({
      ...successful,
      people: [
        johnDoeResponse,
        {
          personId: 2,
          name: 'Jane Roe',
          currentRank: null,
          currentDutyTitle: null,
          careerStartDate: null,
          careerEndDate: null,
        },
      ],
    });

    expect(await people).toEqual([
      johnDoe,
      {
        personId: 2,
        name: 'Jane Roe',
        currentRank: null,
        currentDutyTitle: null,
        careerStartDate: null,
        careerEndDate: null,
      },
    ]);
  });

  it('should get a person by name, encoding the name for the URL', async () => {
    const person = firstValueFrom(api.getPerson('John Doe'));

    const request = http.expectOne('http://api.test/Person/John%20Doe');
    expect(request.request.method).toBe('GET');
    request.flush({ ...successful, person: johnDoeResponse });

    expect(await person).toEqual(johnDoe);
  });

  it("should get a person's duties with calendar dates", async () => {
    const result = firstValueFrom(api.getDuties("Miles O'Brien"));

    const request = http.expectOne("http://api.test/AstronautDuty/Miles%20O'Brien");
    expect(request.request.method).toBe('GET');
    request.flush({
      ...successful,
      person: johnDoeResponse,
      astronautDuties: [
        {
          id: 2,
          rank: '1LT',
          dutyTitle: 'Commander',
          dutyStartDate: '2022-06-01T00:00:00',
          dutyEndDate: null,
        },
        {
          id: 1,
          rank: '2LT',
          dutyTitle: 'Pilot',
          dutyStartDate: '2020-01-01T00:00:00',
          dutyEndDate: '2022-05-31T00:00:00',
        },
      ],
    });

    expect(await result).toEqual({
      person: johnDoe,
      duties: [
        {
          id: 2,
          rank: '1LT',
          dutyTitle: 'Commander',
          dutyStartDate: '2022-06-01',
          dutyEndDate: null,
        },
        {
          id: 1,
          rank: '2LT',
          dutyTitle: 'Pilot',
          dutyStartDate: '2020-01-01',
          dutyEndDate: '2022-05-31',
        },
      ],
    });
  });

  it('should create a person, sending the name as a JSON string', async () => {
    const id = firstValueFrom(api.createPerson('John Doe'));

    const request = http.expectOne('http://api.test/Person');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBe('"John Doe"');
    expect(request.request.headers.get('Content-Type')).toBe('application/json');
    request.flush({ ...successful, id: 7 });

    expect(await id).toBe(7);
  });

  it('should rename a person, sending the new name as a JSON string', async () => {
    const id = firstValueFrom(api.renamePerson('John Doe', 'Jane Doe'));

    const request = http.expectOne('http://api.test/Person/John%20Doe');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toBe('"Jane Doe"');
    expect(request.request.headers.get('Content-Type')).toBe('application/json');
    request.flush({ ...successful, id: 7 });

    expect(await id).toBe(7);
  });

  it('should add a duty, sending the start date as the local calendar date', async () => {
    const id = firstValueFrom(
      api.addDuty({
        name: 'John Doe',
        rank: '1LT',
        dutyTitle: 'Commander',
        dutyStartDate: new Date(2022, 5, 1),
      }),
    );

    const request = http.expectOne('http://api.test/AstronautDuty');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'John Doe',
      rank: '1LT',
      dutyTitle: 'Commander',
      dutyStartDate: '2022-06-01',
    });
    request.flush({ ...successful, id: 3 });

    expect(await id).toBe(3);
  });

  it('should get as many process log entries as asked for, with their time', async () => {
    const entries = firstValueFrom(api.getProcessLog(10));

    const request = http.expectOne('http://api.test/ProcessLog?take=10');
    expect(request.request.method).toBe('GET');
    request.flush({
      ...successful,
      entries: [
        {
          id: 5,
          timestampUtc: '2026-10-23T08:00:00Z',
          level: 'Warning',
          operation: 'CreatePerson',
          message: "A person named 'John Doe' already exists.",
          requestJson: '{"name":"John Doe"}',
          durationMs: 4,
        },
      ],
    });

    expect(await entries).toEqual([
      {
        id: 5,
        timestamp: new Date(Date.UTC(2026, 9, 23, 8)),
        level: 'Warning',
        operation: 'CreatePerson',
        message: "A person named 'John Doe' already exists.",
        requestJson: '{"name":"John Doe"}',
        durationMs: 4,
      },
    ]);
  });

  it('should leave the number of process log entries to the API when not asked', async () => {
    const entries = firstValueFrom(api.getProcessLog());

    http.expectOne('http://api.test/ProcessLog').flush({ ...successful, entries: [] });

    expect(await entries).toEqual([]);
  });

  describe('when a call fails', () => {
    it("should pass on the API's message", async () => {
      const id = firstValueFrom(api.createPerson('John Doe'));

      http.expectOne('http://api.test/Person').flush(
        {
          success: false,
          message: "A person named 'John Doe' already exists.",
          responseCode: 409,
        },
        { status: 409, statusText: 'Conflict' },
      );

      await expect(id).rejects.toBeInstanceOf(ApiError);
      await expect(id).rejects.toMatchObject({
        status: 409,
        message: "A person named 'John Doe' already exists.",
      });
    });

    it('should say the API cannot be reached when there is no response', async () => {
      const people = firstValueFrom(api.getPeople());

      http.expectOne('http://api.test/Person').error(new ProgressEvent('error'));

      await expect(people).rejects.toMatchObject({
        status: 0,
        message: "Can't reach the Stargate API. Check that it's running.",
      });
    });

    it('should fall back to a general message when the response has none', async () => {
      const id = firstValueFrom(api.createPerson('John Doe'));

      http
        .expectOne('http://api.test/Person')
        .flush(
          { title: 'Unsupported Media Type', status: 415 },
          { status: 415, statusText: 'Unsupported Media Type' },
        );

      await expect(id).rejects.toMatchObject({
        status: 415,
        message: 'The Stargate API sent an unexpected response (415).',
      });
    });
  });
});
