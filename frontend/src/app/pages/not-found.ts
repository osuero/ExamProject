import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  template: `<div class="page"><h1>Page not found</h1><p>The address does not match any page. <a routerLink="/exams">Go to the exam list</a>.</p></div>`,
})
export class NotFoundPage {}
