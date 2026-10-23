import { Routes } from '@angular/router';
import { NotFound } from './not-found/not-found';

export const routes: Routes = [
  // The home URL needs its own route, or the wildcard below would match it too.
  { path: '', pathMatch: 'full', redirectTo: 'people' },
  // Pages load on demand, which keeps their Material components out of the starting bundle.
  {
    path: 'people',
    loadComponent: () => import('./people/people').then((m) => m.People),
    title: 'Possible Astronauts – Stargate ACTS',
  },
  { path: '**', component: NotFound, title: 'Page not found – Stargate ACTS' },
];
