import { Routes } from '@angular/router';
import { NotFound } from './not-found/not-found';

export const routes: Routes = [
  // The home URL needs its own route, or the wildcard below would match it too.
  { path: '', pathMatch: 'full', children: [] },
  { path: '**', component: NotFound },
];
