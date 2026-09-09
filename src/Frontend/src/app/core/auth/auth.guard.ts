import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { AuthState } from "./auth-state.service";

export const authGuard: CanActivateFn = (_, state) => {
  const auth = inject(AuthState);
  return auth.isAuthenticated()
    ? true
    : inject(Router).createUrlTree(["/login"], {
        queryParams: { returnUrl: state.url },
      });
};

export const administrationGuard: CanActivateFn = () =>
  inject(AuthState).hasCapability("iam.admin")
    ? true
    : inject(Router).createUrlTree(["/access-denied"]);
