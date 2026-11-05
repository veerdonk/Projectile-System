# Releasing

A merge to `main` that introduces a new `package.json` version runs `.github/workflows/release-package.yml`. It signs the package, creates tag `v{version}` and a GitHub Release with the signed `.tgz` attached, and OpenUPM publishes that tarball ([DECISIONS.md](DECISIONS.md) ADR-054).

## One-time setup (repo owner)

Do these in order. Step 5 must merge before the first signed version reaches `main`.

1. **Unity organization.** Use the organization that should appear as the signer. Copy its **Organization ID** from the [Unity Cloud Dashboard](https://cloud.unity.com) (**Manage organization**).
2. **Service account.** In **Manage organization → Administration → Service Accounts**, select **New**, name it (for example `projectile-system-ci`), and **Create**.
3. **Key and role.** On that account, **Add key** and copy the Key ID and Secret key (shown once). Then **Manage organization roles → Package Manager → Package Manager Package Signer**, and **Save**.
4. **GitHub secrets.** In `veerdonk/Projectile-System` → **Settings → Secrets and variables → Actions → New repository secret**, add:
   - `UPM_SERVICE_ACCOUNT_KEY_ID`
   - `UPM_SERVICE_ACCOUNT_KEY_SECRET`
   - `UPM_ORG_ID`
5. **Test signing.** **Actions → Release package → Run workflow**, tick **sign_only**. The run should pass and leave a `signed-package` artifact. Optional: add that `.tgz` to a project with **Package Manager → + → Install package from tarball** and check that the unsigned warning is gone.
6. **Switch OpenUPM.** Open a PR on [openupm/openupm](https://github.com/openupm/openupm) that edits `data/packages/com.veerdonk.projectile-system.yml` and adds:

   ```yaml
   trackingMode: githubRelease
   ```

   Wait for it to merge.
7. **Release.** Merge the version bump to `main` as usual. Check that the run says `Signed …` and that the GitHub Release has a `.tgz` asset. OpenUPM picks it up shortly after.

## Each release

Unchanged: bump `package.json`, date the `CHANGELOG.md` section, merge to `main`. Never move or delete a released tag.

## If signing fails

- The run fails at **Sign package**: check the three secrets and the signer role. Delete nothing; fix the secrets and rerun the failed job (the tag does not exist yet).
- The run warns that secrets are missing: the release has no asset, and OpenUPM will not publish it. Add the secrets, run the workflow with **sign_only** on `main`, download the `signed-package` artifact, unzip it, and attach the `.tgz` to the existing release with `gh release upload v{version} <file>.tgz`. OpenUPM retries a release without an asset for up to 3 days, so do this within that window. Do not delete the tag.
