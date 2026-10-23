import { Component, input } from '@angular/core';
import { PersonStatus } from '../person-status';

@Component({
  imports: [],
  selector: 'app-status-pill',
  styleUrl: './status-pill.scss',
  templateUrl: './status-pill.html',
})
export class StatusPill {
  readonly status = input.required<PersonStatus>();
}
