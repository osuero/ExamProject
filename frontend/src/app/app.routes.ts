import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/auth';

export const routes: Routes = [
  { path: '', loadComponent: () => import('./pages/home').then((m) => m.HomePage), title: 'Practice Lab' },
  { path: 'exams', loadComponent: () => import('./pages/catalog').then((m) => m.CatalogPage), title: 'Exams · Practice Lab' },
  { path: 'exams/:code', loadComponent: () => import('./pages/exam-detail').then((m) => m.ExamDetailPage), title: 'Exam details · Practice Lab' },
  { path: 'login', loadComponent: () => import('./pages/login').then((m) => m.LoginPage), title: 'Sign in · Practice Lab' },
  { path: 'auth/verify', loadComponent: () => import('./pages/verify').then((m) => m.VerifyPage), title: 'Signing in · Practice Lab' },
  { path: 'attempts/:id', canActivate: [authGuard], loadComponent: () => import('./pages/runner').then((m) => m.RunnerPage), title: 'Attempt · Practice Lab' },
  { path: 'attempts/:id/result', canActivate: [authGuard], loadComponent: () => import('./pages/result').then((m) => m.ResultPage), title: 'Result · Practice Lab' },
  { path: 'my', canActivate: [authGuard], loadComponent: () => import('./pages/my-exams').then((m) => m.MyExamsPage), title: 'My exams · Practice Lab' },
  { path: 'progress/:code', canActivate: [authGuard], loadComponent: () => import('./pages/progress').then((m) => m.ProgressPage), title: 'Progress · Practice Lab' },
  { path: 'admin', canActivate: [adminGuard], loadComponent: () => import('./admin/admin').then((m) => m.AdminPage), title: 'Admin · Practice Lab' },
  { path: 'admin/questions/:id', canActivate: [adminGuard], loadComponent: () => import('./admin/question-detail').then((m) => m.QuestionDetailPage), title: 'Question · Admin' },
  { path: '**', loadComponent: () => import('./pages/not-found').then((m) => m.NotFoundPage), title: 'Not found · Practice Lab' },
];
