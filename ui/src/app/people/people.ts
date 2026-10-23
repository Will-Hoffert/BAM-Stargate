import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { errorMessage } from '../api/api-error';
import { Person } from '../api/api-models';
import { StargateApi } from '../api/stargate-api';
import { PersonNameData, PersonNameDialog } from './person-name-dialog/person-name-dialog';
import { PersonStatus, personStatus } from './person-status';
import { StatusPill } from './status-pill/status-pill';

interface PersonRow extends Person {
  status: PersonStatus;
}

type Column =
  'name' | 'status' | 'currentRank' | 'currentDutyTitle' | 'careerStartDate' | 'careerEndDate';

// Ignores case, like the API's name matching, and puts numbers in order (2 before 10).
const collator = new Intl.Collator(undefined, { sensitivity: 'base', numeric: true });

@Component({
  imports: [
    DatePipe,
    MatButton,
    MatFormField,
    MatHint,
    MatInput,
    MatLabel,
    MatSortModule,
    MatTableModule,
    RouterLink,
    StatusPill,
  ],
  selector: 'app-people',
  styleUrl: './people.scss',
  templateUrl: './people.html',
})
export class People {
  private readonly api = inject(StargateApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  protected readonly columns: Column[] = [
    'name',
    'status',
    'currentRank',
    'currentDutyTitle',
    'careerStartDate',
    'careerEndDate',
  ];
  protected readonly people = rxResource({ stream: () => this.api.getPeople() });
  protected readonly search = signal('');
  protected readonly sort = signal<Sort>({ active: 'name', direction: 'asc' });
  protected readonly errorMessage = errorMessage;

  protected readonly rows = computed(() => {
    // value() throws while the resource holds an error, so check it has one first.
    if (!this.people.hasValue()) return [];
    const term = this.search().trim().toLocaleLowerCase();
    const rows = this.people
      .value()
      .map((person) => ({ ...person, status: personStatus(person) }))
      .filter((row) => matches(row, term));
    return sortRows(rows, this.sort());
  });

  // A new person's next step is their first duty, which is added on their timeline.
  protected addPerson(): void {
    this.dialog
      .open<PersonNameDialog, PersonNameData, string>(PersonNameDialog, {
        width: '400px',
        data: { title: 'New person', name: '', save: (name) => this.api.createPerson(name) },
      })
      .afterClosed()
      .subscribe((name) => {
        if (name !== undefined) void this.router.navigate(['/people', name]);
      });
  }
}

function matches(row: PersonRow, term: string): boolean {
  return [row.name, row.status, row.currentRank, row.currentDutyTitle].some(
    (value) => value !== null && value.toLocaleLowerCase().includes(term),
  );
}

function sortRows(rows: PersonRow[], sort: Sort): PersonRow[] {
  const column = sort.active as Column;
  const direction = sort.direction === 'desc' ? -1 : 1;
  return rows.sort(
    (a, b) => compare(a[column], b[column], direction) || collator.compare(a.name, b.name),
  );
}

// Blanks go last whichever way the column is sorted.
function compare(a: string | null, b: string | null, direction: number): number {
  if (a === b) return 0;
  if (a === null) return 1;
  if (b === null) return -1;
  return direction * collator.compare(a, b);
}
