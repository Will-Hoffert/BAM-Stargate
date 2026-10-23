import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { MatSortHarness } from '@angular/material/sort/testing';
import { MatTableHarness } from '@angular/material/table/testing';
import { provideRouter } from '@angular/router';
import { Observable, Subject, of, throwError } from 'rxjs';
import { ApiError } from '../api/api-error';
import { Person } from '../api/api-models';
import { StargateApi } from '../api/stargate-api';
import { People } from './people';

const commander: Person = {
  personId: 1,
  name: 'John Doe',
  currentRank: '1LT',
  currentDutyTitle: 'Commander',
  careerStartDate: '2020-01-01',
  careerEndDate: null,
};

const retiree: Person = {
  personId: 2,
  name: 'Amy Ray',
  currentRank: 'COL',
  currentDutyTitle: 'RETIRED',
  careerStartDate: '2001-03-15',
  careerEndDate: '2024-12-31',
};

const newcomer: Person = {
  personId: 3,
  name: 'Zed Park',
  currentRank: null,
  currentDutyTitle: null,
  careerStartDate: null,
  careerEndDate: null,
};

function configure(getPeople: () => Observable<Person[]>) {
  TestBed.configureTestingModule({
    imports: [People],
    providers: [provideRouter([]), { provide: StargateApi, useValue: { getPeople } }],
  });
}

async function render(getPeople: () => Observable<Person[]>) {
  configure(getPeople);
  const fixture = TestBed.createComponent(People);
  await fixture.whenStable();
  return {
    element: fixture.nativeElement as HTMLElement,
    loader: TestbedHarnessEnvironment.loader(fixture),
  };
}

async function column(loader: HarnessLoader, name: string): Promise<string[]> {
  const table = await loader.getHarness(MatTableHarness);
  return (await table.getCellTextByColumnName())[name].text;
}

describe('People', () => {
  it('should list everyone with their status, sorted by name', async () => {
    const { loader } = await render(() => of([commander, retiree, newcomer]));

    expect(await column(loader, 'name')).toEqual(['Amy Ray', 'John Doe', 'Zed Park']);
    expect(await column(loader, 'status')).toEqual(['Retired', 'Active', 'Not an astronaut yet']);
    expect(await column(loader, 'currentRank')).toEqual(['COL', '1LT', '']);
    expect(await column(loader, 'currentDutyTitle')).toEqual(['RETIRED', 'Commander', '']);
    const [nameHeader] = await (
      await loader.getHarness(MatSortHarness)
    ).getSortHeaders({
      label: 'Name',
    });
    expect(await nameHeader.getSortDirection()).toBe('asc');
  });

  it("should link each name to that person's timeline", async () => {
    const { element } = await render(() => of([commander, retiree, newcomer]));

    const links = [...element.querySelectorAll('td a')].map((a) => a.getAttribute('href'));
    expect(links).toEqual(['/people/Amy%20Ray', '/people/John%20Doe', '/people/Zed%20Park']);
  });

  it('should show career dates as the calendar dates they are', async () => {
    const { loader } = await render(() => of([commander, retiree, newcomer]));

    expect(await column(loader, 'careerStartDate')).toEqual(['Mar 15, 2001', 'Jan 1, 2020', '']);
    expect(await column(loader, 'careerEndDate')).toEqual(['Dec 31, 2024', '', '']);
  });

  it('should sort by a column when its header is clicked, keeping blanks last', async () => {
    const { loader } = await render(() => of([commander, retiree, newcomer]));
    const [rankHeader] = await (
      await loader.getHarness(MatSortHarness)
    ).getSortHeaders({
      label: 'Current rank',
    });

    await rankHeader.click();
    expect(await column(loader, 'name')).toEqual(['John Doe', 'Amy Ray', 'Zed Park']);

    await rankHeader.click();
    expect(await column(loader, 'name')).toEqual(['Amy Ray', 'John Doe', 'Zed Park']);
  });

  it('should narrow the list by name, rank, duty title or status', async () => {
    const { loader } = await render(() => of([commander, retiree, newcomer]));
    const search = await loader.getHarness(MatInputHarness);

    await search.setValue('ray');
    expect(await column(loader, 'name')).toEqual(['Amy Ray']);

    await search.setValue('1lt');
    expect(await column(loader, 'name')).toEqual(['John Doe']);

    await search.setValue('COMMANDER');
    expect(await column(loader, 'name')).toEqual(['John Doe']);

    await search.setValue('retired');
    expect(await column(loader, 'name')).toEqual(['Amy Ray']);

    await search.setValue('not an astronaut');
    expect(await column(loader, 'name')).toEqual(['Zed Park']);

    await search.setValue('');
    expect(await column(loader, 'name')).toEqual(['Amy Ray', 'John Doe', 'Zed Park']);
  });

  it('should say when no one matches the search', async () => {
    const { loader } = await render(() => of([commander, retiree, newcomer]));

    await (await loader.getHarness(MatInputHarness)).setValue('xyz');

    const noDataRow = await (await loader.getHarness(MatTableHarness)).getNoDataRow();
    expect(await noDataRow?.getCellTextByIndex()).toEqual(['No one matches "xyz".']);
  });

  it('should say when there are no people yet', async () => {
    const { element, loader } = await render(() => of([]));

    expect(element.textContent).toContain('No people yet.');
    expect(await loader.getHarnessOrNull(MatTableHarness)).toBeNull();
  });

  // A resource that is still loading keeps the fixture from becoming stable, so this test
  // renders once instead of waiting for it.
  it('should not say there are no people while they are loading', () => {
    configure(() => new Subject<Person[]>());
    const fixture = TestBed.createComponent(People);
    fixture.detectChanges();

    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).not.toContain('No people yet.');
    expect(element.querySelector('table')).toBeNull();
  });

  it('should show why loading failed, and load again on Try again', async () => {
    let calls = 0;
    const { element, loader } = await render(() =>
      ++calls === 1
        ? throwError(
            () => new ApiError(0, "Can't reach the Stargate API. Check that it's running."),
          )
        : of([commander]),
    );

    expect(element.querySelector('[role="alert"]')?.textContent).toContain(
      "Can't reach the Stargate API. Check that it's running.",
    );

    await (await loader.getHarness(MatButtonHarness.with({ text: 'Try again' }))).click();

    expect(await column(loader, 'name')).toEqual(['John Doe']);
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });
});
