import { redirect } from "next/navigation";
import { currentIdentity, identityUiEnabled } from "../../lib/identity";
import ConsoleShell from "../components/console-shell";

export const dynamic = "force-dynamic";
export default async function ConsoleLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  if (!identityUiEnabled()) redirect("/");
  const actor = await currentIdentity();
  if (!actor) redirect("/identity");
  return <ConsoleShell roles={actor.roles} userId={actor.userId}>{children}</ConsoleShell>;
}
