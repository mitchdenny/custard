# Publishing `@hex1b/web-terminal`

`@hex1b/web-terminal` is the experimental browser client for Hex1b's
[HWT1 protocol](web-terminal-protocol.md). Use it with the **matching Hex1b NuGet
version from the same build**. It is a paired client, not an independently
versioned protocol implementation: there is no promise of wire compatibility
between arbitrary browser-client and server versions.

The package name is **`@hex1b/web-terminal` in both registries**. Its source
repository remains `mitchdenny/hex1b`, and its `package.json` contains:

```json
{
  "name": "@hex1b/web-terminal",
  "repository": {
    "type": "git",
    "url": "git+https://github.com/mitchdenny/hex1b.git"
  }
}
```

Do not add a fixed `publishConfig.registry` or `publishConfig.tag`: the workflow
chooses the destination and channel explicitly.

## Versions and channels

[Deploy](../.github/workflows/build-deploy.yml) computes a version once using the
existing [version action](../.github/actions/version/action.yml). The npm build
consumes **exactly `needs.version.outputs.version`**, just like the NuGet builds.
It does not derive a separate npm version from tags, commits, or `package.json`.

| Build | Registry | Version from the common version job | npm dist-tag |
| --- | --- | --- | --- |
| Same-repository PR | `https://npm.pkg.github.com` | `BASE-pr.NUM.RUN.ATTEMPT.SHA` | `pr-NUM` |
| `main` | `https://registry.npmjs.org` | `BASE-alpha.RUN.ATTEMPT.SHA` | `alpha` |
| `release/X.Y` preview | `https://registry.npmjs.org` | `BASE-beta.RUN.ATTEMPT.SHA` | `beta` |
| Manual dispatch on `release/X.Y` with `release=true` | `https://registry.npmjs.org` | `BASE` | `latest` |

`BASE`, the PR number, run number, attempt, and short SHA are all supplied by the
existing version action. Prereleases never move `latest`. Fork and Dependabot
PRs still build and test, but **do not run the credential-bearing publish job**.
Publishing jobs run only in the canonical repository.
If a short SHA is entirely numeric and starts with zero, the shared action
prefixes that identifier with `g` to satisfy npm's SemVer rules. This
normalization applies to both npm and NuGet, not just one distribution.

The dedicated `build-web-terminal` job uses Node.js 24 to install dependencies,
stamp both package manifests using
`npm version "$PACKAGE_VERSION" --no-git-tag-version --allow-same-version --ignore-scripts`,
build, test, and pack. It also restores `samples/WebTerminalDemo` and uses the
sample's build command to compile the library once, check the playground's
TypeScript consumer, and copy its browser assets. It then tests and packs that
same library build, uploading the tested `.tgz` as `npm-web-terminal`.
The .NET test/package jobs depend on that job. Publishing downloads this artifact
and uses `npm publish <tarball> --ignore-scripts`; it does not install development
dependencies, rebuild, or run package lifecycle scripts with publishing credentials.
No npm version-stamping commit or git tag is created.

## One-time manual npmjs bootstrap: `0.1.0`

First, ensure your **npmjs** account has permission to publish public packages in
the npm organization `hex1b`, and configure interactive login and two-factor
authentication. This npm organization is independent of any GitHub organization.

Leave the repository Actions variable `NPM_PUBLISH_ENABLED` unset or set to
`false`. This disables only automated **npmjs** publication; build artifacts,
NuGet publishing, and the configured GitHub Packages preview flow remain available.

From the repository root, using Node.js 24 and npm **11.5.1 or newer**:

```bash
cd src/web-terminal
npm ci --registry=https://registry.npmjs.org
npm version 0.1.0 --no-git-tag-version --allow-same-version --ignore-scripts
npm run build
npm test
mkdir -p ../../artifacts/npm-bootstrap
npm pack --ignore-scripts --pack-destination ../../artifacts/npm-bootstrap --registry=https://registry.npmjs.org

# Inspect the exact tarball you will publish.
tar -tzf ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz
tar -xOf ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz package/package.json
npm publish ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz --dry-run --ignore-scripts --access public --tag latest --registry=https://registry.npmjs.org
```

Verify that the package contains `dist/index.js`, TypeScript declarations,
relative worker/modules/font assets, README, and MIT license, and does not
contain credentials, development dependencies, or unrelated sample files.
`prepack` also supports ordinary `npm pack`; the commands above skip lifecycle
scripts because you already built and tested the contents.

Then, **only when ready to publish**, authenticate interactively and publish that
same reviewed tarball:

```bash
npm login --scope=@hex1b --registry=https://registry.npmjs.org
npm whoami --registry=https://registry.npmjs.org
npm publish ../../artifacts/npm-bootstrap/hex1b-web-terminal-0.1.0.tgz --ignore-scripts --access public --tag latest --registry=https://registry.npmjs.org
npm view @hex1b/web-terminal@0.1.0 version --registry=https://registry.npmjs.org
```

Complete npm's browser/2FA prompts. Do not put an OTP or login credential into
the repository or a CI secret. Check any existing local `@hex1b:registry` mapping:
it must point to npmjs for this bootstrap, not GitHub Packages.

This initial `0.1.0` publication creates the npm package so you can configure its
trusted publisher. It is **independent of the common CI version stream** and
does not imply compatibility with a historical Hex1b NuGet `0.1.0`; use its
corresponding source build until paired CI packages are available.
**Do not create a `v0.1.0` release git tag for this bootstrap.** Release tags are
inputs to NuGet's version action and would affect the shared version stream.
Do not reset subsequent CI versions to `0.1.0`.

## Enable npm trusted publishing

After the bootstrap, open `@hex1b/web-terminal` on npmjs → **Settings** →
**Trusted publishing / Trusted Publisher**, select **GitHub Actions**, and enter:

| npm setting | Exact value |
| --- | --- |
| Organization or user | `mitchdenny` |
| Repository | `hex1b` |
| Workflow filename | `build-deploy.yml` |
| Environment name | `production` |

Use only the workflow filename, not `.github/workflows/build-deploy.yml`.
If the settings form offers allowed actions, allow direct **`npm publish`**;
this workflow does not use staged publishing.
The organization/user here identifies the **GitHub source repository owner**,
not the npm organization `hex1b`. Keep `repository.url` exactly as shown above.

The workflow uses a GitHub-hosted Ubuntu runner, Node.js 24, and
`id-token: write` in the `production` environment. It verifies npm is at least
11.5.1 before publishing. npm exchanges the OIDC identity for short-lived
credentials; **no npmjs access-token secret or `NODE_AUTH_TOKEN` is configured**.
Trusted publishing automatically supplies provenance for eligible public
repositories/packages.

Configure the GitHub `production` environment's review and branch protections
as appropriate, ensuring the intended `main` and `release/X.Y` publications are
allowed. Then set the repository Actions variable **`NPM_PUBLISH_ENABLED=true`**
to activate npmjs publishing. This guide and workflow do not configure accounts,
secrets, variables, or trusted publishers for you.

After verifying trusted publication works, npm recommends **Require two-factor
authentication and disallow tokens** in the package's publishing-access settings.
This does not disable trusted publishing.

See npm's [trusted-publisher documentation](https://docs.npmjs.com/trusted-publishers/)
for current requirements and troubleshooting.

## GitHub Packages preview prerequisites

On GitHub Packages, the `@hex1b` scope refers to a **GitHub** account or
organization named `hex1b`. Owning an npmjs organization named `hex1b` does not
create that GitHub namespace or grant permissions to it.

Before expecting PR publication to succeed:

1. Confirm the GitHub `hex1b` namespace exists, is controlled by the intended
   owner, and allows this package and publisher.
2. Verify GitHub accepts the package's association with the canonical
   `mitchdenny/hex1b` repository. GitHub's npm registry uses `repository.url`
   for repository association. Its documentation distinguishes repository
   association, inherited permissions, and **Manage Actions access**, but does
   **not guarantee this cross-owner association**. Do not assume a token alone
   bypasses namespace or repository-association restrictions. If GitHub rejects
   this arrangement, resolve the supported setup with the namespace administrator
   or GitHub Support before relying on preview publication. The workflow does
   not rename the package, change its repository URL, or strip its metadata.
3. Where GitHub supports the association/access arrangement, grant the workflow
   repository appropriate package write access. Without an override, the job
   uses its `github.token` with `packages: write`.
4. If a separate authorized identity is required, configure the repository
   secret **`NPM_GITHUB_PACKAGES_TOKEN`** with a **classic PAT**, granting
   `write:packages` and `read:packages` and the required namespace/package
   permissions (including organization authorization where required).
   The workflow uses this token in preference to `github.token`, only in the
   GitHub npm publish step. A PAT is an authentication option, **not a guarantee
   of cross-owner publication**.
5. Configure package visibility and consumer access on GitHub. npm packages
   default to private there; even public npm packages on GitHub require
   authentication to install.

These are external setup prerequisites, not completed by this change. An
incompatible GitHub namespace/repository setup blocks preview publication
without changing the required package name. npmjs trusted publishing is
configured separately and does not use this PAT.

To consume a PR preview, authenticate using a GitHub username and a classic
PAT with `read:packages` when prompted, then install the exact version printed
by the PR workflow (replace the example below with that version):

```bash
npm login --scope=@hex1b --auth-type=legacy --registry=https://npm.pkg.github.com
npm install @hex1b/web-terminal@0.43.0-pr.123.202.1.abc1234 --save-exact --registry=https://npm.pkg.github.com
```

This login may create a user-level `@hex1b:registry` mapping. Switch it back with
`npm config set @hex1b:registry https://registry.npmjs.org --location=user` when
returning to npmjs, and check for project-level mappings too. Keep registry
credentials outside committed files. A moving `pr-123` tag is convenient for
discovery, but pin the resolved version alongside matching NuGet packages.

See GitHub's official guides for the
[npm registry and authentication](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-npm-registry),
[repository association](https://docs.github.com/en/packages/learn-github-packages/connecting-a-repository-to-a-package),
and [package/Actions access](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility).

## Failure handling and retries

The workflow serializes publishing per branch. The existing stale-beta check
applies to **both** npm and NuGet: if the final `vBASE` tag already exists, neither
publishes that beta.

For npm, the helper first queries the exact package version in the selected
registry. An already published version is skipped; only an npm `E404`/HTTP 404
means absent. Authentication, authorization, network, rate-limit, malformed
responses, and server failures stop the job rather than trigger a blind publish.
GitHub may hide inaccessible packages behind a 404, so verify permissions when
troubleshooting an unexpected publish rejection.

npm publication runs **before NuGet**. If npm fails, NuGet has not been pushed by
that job; if NuGet subsequently fails, retrying the failed publish job reuses the
artifact, skips an existing npm version, and uses NuGet's duplicate-safe push.
Existing npm versions are not republished or retagged on retry. Review any
partially completed stable GitHub release/baseline dispatch separately.

Prefer rerunning **failed jobs**, retaining the successful version/build job
outputs and artifacts. Rerunning the whole workflow can compute a different
version because attempt numbers and release tags are inputs to the version
action. npm does not permit reusing a published name/version, even after
unpublishing.

To test publication decisions locally without publishing or registry requests:

```bash
node --test .github/scripts/publish-web-terminal.test.mjs
```

Further npm references:
[publishing scoped public packages](https://docs.npmjs.com/creating-and-publishing-scoped-public-packages/),
[`npm publish`, dry runs, and version immutability](https://docs.npmjs.com/cli/v11/commands/npm-publish/),
and [dist-tags](https://docs.npmjs.com/adding-dist-tags-to-packages/).
