import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { packageName, repositoryUrl, publicationTarget, validateManifest, versionExists, publishPackage } from './publish-web-terminal.mjs';

const manifestFor = version => ({ name: packageName, version, repository: { url: repositoryUrl } });
const response = (status, body) => ({ status, stdout: JSON.stringify(body), stderr: '' });

for (const [kind, version, tag, registry, prNumber] of [
    ['release', '0.42.0', 'latest', 'https://registry.npmjs.org'],
    ['alpha', '0.43.0-alpha.200.1.abc1234', 'alpha', 'https://registry.npmjs.org'],
    ['beta', '0.42.0-beta.201.2.abc1234', 'beta', 'https://registry.npmjs.org'],
    ['pr', '0.43.0-pr.123.202.1.abc1234', 'pr-123', 'https://npm.pkg.github.com', '123'],
]) {
    test(`${kind} keeps the shared version and uses the correct registry and dist-tag`, () => {
        assert.deepEqual(publicationTarget(version, kind, prNumber), { tag, registry });
        const calls = [];
        const tarball = `/artifacts/${version}.tgz`;
        const published = publishPackage({ version, kind, prNumber, tarball, manifest: manifestFor(version) }, args => {
            calls.push(args);
            return args[0] === 'view' ? response(1, { error: { code: 'E404' } }) : response(0, {});
        });
        assert.equal(published, true);
        assert.deepEqual(calls[0], ['view', `${packageName}@${version}`, 'version', '--json', '--registry', registry]);
        assert.deepEqual(calls[1], [
            'publish', tarball, '--ignore-scripts', '--registry', registry, '--tag', tag,
            ...(kind === 'pr' ? [] : ['--access', 'public']),
        ]);
    });
}

test('invalid kind/version pairs cannot publish prereleases as latest', () => {
    for (const args of [
        ['0.42.0-beta.1', 'release'], ['0.42.0', 'alpha'], ['0.42.0-alpha.1', 'beta'],
        ['0.42.0-pr.12.1', 'pr', '13'], ['0.42.0-pr.12.1', 'pr', ''],
        ['0.42.0', 'unknown'], ['--tag=latest', 'release'], [undefined, 'release'],
    ]) {
        assert.throws(() => publicationTarget(...args));
    }
});

test('the shared version action keeps numeric SHA identifiers valid for npm and NuGet', () => {
    const action = readFileSync(new URL('../actions/version/action.yml', import.meta.url), 'utf8');
    const script = action.split('      run: |\n')[1].split('\n').map(line => line.replace(/^        /, '')).join('\n');
    const root = mkdtempSync(resolve('.github/scripts/.version-fixture-'));
    try {
        execFileSync('git', ['init', '--quiet', root]);
        for (const [sha, expectedSuffix] of [
            ['0012345abcdef', 'g0012345'],
            ['0000000abcdef', 'g0000000'],
            ['1234567abcdef', '1234567'],
            ['0abcdef123456', '0abcdef'],
        ]) {
            const output = resolve(root, 'outputs');
            writeFileSync(output, '');
            execFileSync('bash', ['-c', script], {
                cwd: root,
                env: {
                    ...process.env,
                    GIT_CONFIG_NOSYSTEM: '1',
                    GIT_CONFIG_GLOBAL: '/dev/null',
                    GITHUB_OUTPUT: output,
                    EVENT_NAME: 'pull_request',
                    BASE_REF: 'main',
                    REF_NAME: '',
                    PR_NUMBER: '485',
                    RUN_NUMBER: '123',
                    RUN_ATTEMPT: '1',
                    GIT_SHA: sha,
                    RELEASE_FLAG: 'false',
                },
                stdio: 'pipe',
            });
            const version = readFileSync(output, 'utf8').match(/^version=(.+)$/m)[1];
            assert.equal(version, `0.1.0-pr.485.123.1.${expectedSuffix}`);
            for (const identifier of version.split('-')[1].split('.')) {
                assert.doesNotMatch(identifier, /^0\d+$/, 'Numeric SemVer identifiers cannot have leading zeroes');
            }
            assert.equal(publicationTarget(version, 'pr', '485').tag, 'pr-485');
        }
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

test('tarball metadata must match the fixed name, common version and canonical repository', () => {
    const version = '0.42.0';
    validateManifest(manifestFor(version), version);
    for (const overrides of [
        { name: '@other/web-terminal' }, { version: '0.1.0' },
        { repository: { url: 'git+https://github.com/other/hex1b.git' } },
        { publishConfig: { registry: 'https://npm.pkg.github.com' } },
        { publishConfig: { tag: 'latest' } },
    ]) {
        assert.throws(() => validateManifest({ ...manifestFor(version), ...overrides }, version));
    }
});

test('retry skips an already published exact version without changing a dist-tag', () => {
    const version = '0.42.0';
    const calls = [];
    assert.equal(publishPackage({
        version, kind: 'release', tarball: '/artifacts/package.tgz', manifest: manifestFor(version),
    }, args => {
        calls.push(args);
        return response(0, version);
    }), false);
    assert.equal(calls.length, 1);
    assert.equal(calls[0][0], 'view');
});

test('only E404 or HTTP 404 means absent', () => {
    for (const code of ['E404', '404', 404]) {
        assert.equal(versionExists('0.42.0', 'https://registry.npmjs.org',
            () => response(1, { error: { code } })), false);
    }
});

for (const code of ['E401', 'E403', 'E429', 'E500', 'E503', 'ENOTFOUND', 'ECONNRESET', 'ETIMEDOUT']) {
    test(`${code} prevents publishing instead of assuming the version is absent`, () => {
        const version = '0.42.0';
        const calls = [];
        assert.throws(() => publishPackage({
            version, kind: 'release', tarball: '/artifacts/package.tgz', manifest: manifestFor(version),
        }, args => {
            calls.push(args);
            return response(1, { error: { code } });
        }), /npm view failed/);
        assert.equal(calls.length, 1);
    });
}

test('unexpected success payloads, non-JSON errors and terminated processes fail closed', () => {
    for (const result of [
        response(0, '0.41.0'), response(0, null), response(0, []), response(0, {}),
        response(1, {}), { status: 1, stdout: '<html>503</html>' }, { status: null, signal: 'SIGTERM' },
        { error: new Error('npm missing') },
    ]) {
        assert.throws(() => versionExists('0.42.0', 'https://registry.npmjs.org', () => result));
    }
});

test('publish failures are propagated, including a concurrent duplicate or missing npm', () => {
    const version = '0.42.0';
    for (const result of [response(1, {}), { status: null, signal: 'SIGTERM' }, { error: new Error('npm missing') }]) {
        assert.throws(() => publishPackage({
            version, kind: 'release', tarball: '/artifacts/package.tgz', manifest: manifestFor(version),
        }, args => args[0] === 'view' ? response(1, { error: { code: 'E404' } }) : result));
    }
});

test('artifact verification reads the packed manifest without publishing', () => {
    const root = resolve(`.github/scripts/.publish-web-terminal-fixture-${process.pid}`);
    const version = '0.42.0-beta.201.2.abc1234';
    const script = fileURLToPath(new URL('./publish-web-terminal.mjs', import.meta.url));
    try {
        mkdirSync(`${root}/package`, { recursive: true });
        mkdirSync(`${root}/artifacts/npm`, { recursive: true });
        writeFileSync(`${root}/package/package.json`, JSON.stringify(manifestFor(version)));
        execFileSync('tar', ['-czf', `artifacts/npm/hex1b-web-terminal-${version}.tgz`, 'package'], { cwd: root });
        const output = execFileSync(process.execPath, [script, '--verify'], {
            cwd: root,
            env: { ...process.env, PACKAGE_VERSION: version, PACKAGE_KIND: 'beta' },
            encoding: 'utf8',
        });
        assert.match(output, /Verified @hex1b\/web-terminal@0\.42\.0-beta\.201\.2\.abc1234/);
    } finally {
        rmSync(root, { recursive: true, force: true });
    }
});

const workflow = readFileSync(new URL('../workflows/build-deploy.yml', import.meta.url), 'utf8');
function workflowJob(name) {
    const job = workflow.match(new RegExp(`^  ${name}:\\n[\\s\\S]*?(?=^  [\\w-]+:|$(?![\\s\\S]))`, 'm'));
    assert.ok(job, `Missing workflow job ${name}`);
    return job[0];
}

test('workflow builds and packs once with the exact shared version and gates .NET packages', () => {
    const build = workflowJob('build-web-terminal');
    assert.match(build, /needs: version/);
    assert.match(build, /contents: read/);
    assert.match(build, /node-version: '24'/);
    assert.match(build, /PACKAGE_VERSION: \$\{\{ needs\.version\.outputs\.version \}\}/);
    assert.match(build, /npm version "\$PACKAGE_VERSION" --no-git-tag-version --allow-same-version --ignore-scripts/);
    assert.equal((build.match(/npm run build/g) ?? []).length, 1);
    assert.match(build, /npm ci --prefix samples\/WebTerminalDemo --registry=https:\/\/registry\.npmjs\.org/);
    assert.match(build, /npm run build --prefix samples\/WebTerminalDemo/);
    assert.ok(build.indexOf('npm run build --prefix samples/WebTerminalDemo') < build.indexOf('run: npm test'));
    assert.equal((build.match(/npm pack /g) ?? []).length, 1);
    assert.match(build, /npm pack --ignore-scripts/);
    assert.match(build, /publish-web-terminal\.mjs --verify/);
    assert.match(build, /name: npm-web-terminal/);
    for (const job of ['test', 'build-pr', 'build-release']) {
        assert.match(workflowJob(job), /needs: [^\n]*build-web-terminal/);
    }
});

test('each publisher depends only on the common version and build, never another publisher', () => {
    for (const registry of ['nuget', 'npm']) {
        for (const kind of ['pr', 'release']) {
            const job = workflowJob(`publish-${registry}-${kind}`);
            assert.match(job, new RegExp(`needs: \\[version, build-${kind}\\]`));
            assert.match(job, /contents: read/);
            assert.doesNotMatch(job, /contents: write|actions: write/);
            assert.match(job, /needs\.version\.outputs\.version/);
            assert.match(job, new RegExp(`name: ${registry === 'npm' ? 'npm-web-terminal' : `nuget-packages-${kind}`}`));
        }
    }
    assert.doesNotMatch(workflow, /^  publish-(pr|release):/m);
});

test('both PR publishers guard credentials against forks and Dependabot', () => {
    for (const registry of ['nuget', 'npm']) {
        const pr = workflowJob(`publish-${registry}-pr`);
        for (const guard of [
            "github.event_name == 'pull_request'",
            "github.repository == 'mitchdenny/hex1b'",
            'github.event.pull_request.head.repo.full_name == github.repository',
            "github.event.pull_request.user.login != 'dependabot[bot]'",
            "github.actor != 'dependabot[bot]'",
            "github.triggering_actor != 'dependabot[bot]'",
        ]) assert.ok(pr.includes(guard), `Missing ${registry} PR guard: ${guard}`);
        assert.match(pr, /packages: write/);
        assert.match(pr, /pull-requests: write/);
        assert.doesNotMatch(pr, /id-token: write/);
        assert.doesNotMatch(pr, /npm (ci|install|run build|pack)(?:\n| --)/);
    }
    const npm = workflowJob('publish-npm-pr');
    assert.match(npm, /NODE_AUTH_TOKEN: \$\{\{ secrets\.NPM_GITHUB_PACKAGES_TOKEN \|\| github\.token \}\}/);
    assert.match(npm, /scope: '@hex1b'/);
    assert.match(npm, /registry-url: https:\/\/npm\.pkg\.github\.com/);
    assert.doesNotMatch(workflowJob('publish-nuget-pr'), /NODE_AUTH_TOKEN|NPM_GITHUB_PACKAGES_TOKEN/);
});

test('PR success notices use distinct markers and report only their own registry', () => {
    const nuget = workflowJob('publish-nuget-pr');
    const npm = workflowJob('publish-npm-pr');
    assert.match(nuget, /startswith\("<!-- hex1b-pr-nuget -->"\)/);
    assert.match(nuget, /## 📦 PR NuGet Packages Published/);
    assert.match(nuget, /npm publication is reported separately and may still be pending or failed/);
    assert.doesNotMatch(nuget, /hex1b-pr-npm|@hex1b\/web-terminal|## 📦 PR Packages Published/);
    assert.match(npm, /startswith\("<!-- hex1b-pr-npm -->"\)/);
    assert.match(npm, /## 📦 PR npm Package Published/);
    assert.match(npm, /NuGet publication is reported separately and may still be pending or failed/);
    assert.doesNotMatch(npm, /hex1b-pr-nuget|dotnet nuget push|nuget-packages-pr/);
    assert.ok(nuget.indexOf('dotnet nuget push') < nuget.indexOf('Post package info to PR'));
    assert.ok(npm.indexOf('run: node .github/scripts/publish-web-terminal.mjs') < npm.indexOf('Post npm package info to PR'));
});

test('npmjs keeps tokenless production OIDC, explicit registry, opt-in, and publish-only verbose diagnostics', () => {
    const release = workflowJob('publish-npm-release');
    assert.match(release, /environment: production/);
    assert.match(release, /id-token: write/);
    assert.match(release, /node-version: '24'/);
    assert.match(release, /npm >= 11\.5\.1/);
    assert.doesNotMatch(release, /NODE_AUTH_TOKEN|NPM_GITHUB_PACKAGES_TOKEN|NPM_TOKEN|registry-url: https:\/\/npm\.pkg/);
    assert.match(release, /registry-url: https:\/\/registry\.npmjs\.org/);
    assert.match(release, /vars\.NPM_PUBLISH_ENABLED == 'true'[\s\S]*environment: production/);
    assert.match(release, /Publish to npmjs with trusted publishing\n\s+if: steps\.stale-check\.outputs\.skip != 'true'/);
    assert.match(release, /Publish to npmjs[\s\S]*env:[\s\S]*NPM_CONFIG_LOGLEVEL: verbose/);
    assert.equal((workflow.match(/NPM_CONFIG_LOGLEVEL/g) ?? []).length, 1);
    assert.match(release, /PACKAGE_VERSION: \$\{\{ needs\.version\.outputs\.version \}\}/);
    assert.ok(release.indexOf('Skip stale beta') < release.indexOf('Publish to npmjs'));
    assert.doesNotMatch(release, /npm (ci|install|run build|pack)(?:\n| --)/);
    assert.doesNotMatch(release, /nuget\/login|dotnet nuget push|gh release/);
});

test('NuGet keeps its own production OIDC and duplicate-safe push without npm credentials', () => {
    const nuget = workflowJob('publish-nuget-release');
    assert.match(nuget, /environment: production/);
    assert.match(nuget, /id-token: write/);
    assert.match(nuget, /uses: nuget\/login@v1/);
    assert.match(nuget, /--skip-duplicate/);
    assert.doesNotMatch(nuget, /NPM_PUBLISH_ENABLED|NPM_CONFIG|NODE_AUTH_TOKEN|npm-web-terminal|gh release/);
});

test('stable finalization waits for both registries, allowing only an explicitly disabled npm job to skip', () => {
    const finalizer = workflowJob('finalize-release');
    assert.match(finalizer, /needs: \[version, publish-nuget-release, publish-npm-release\]/);
    assert.match(finalizer, /always\(\) && !cancelled\(\)/);
    assert.match(finalizer, /needs\.version\.result == 'success'/);
    assert.match(finalizer, /needs\.version\.outputs\.kind == 'release'/);
    assert.match(finalizer, /needs\.publish-nuget-release\.result == 'success'/);
    assert.match(finalizer, /\(needs\.publish-npm-release\.result == 'success' \|\|\s*\(vars\.NPM_PUBLISH_ENABLED != 'true' && needs\.publish-npm-release\.result == 'skipped'\)\)/);
    assert.match(finalizer, /contents: write/);
    assert.match(finalizer, /actions: write/);
    assert.doesNotMatch(finalizer, /id-token: write|check-package|nugetlogin|NODE_AUTH_TOKEN/);
    assert.match(finalizer, /Create GitHub Release\n\s+if: steps\.check-release\.outputs\.exists == 'false'/);
    assert.match(finalizer, /--target "\$GITHUB_SHA"/);
    assert.match(finalizer, /gh release upload[\s\S]*--clobber/);
    assert.match(finalizer, /\.\/artifacts\/\*\.nupkg \.\/artifacts\/npm\/\*\.tgz/);
    assert.match(finalizer, /gh workflow run baseline-bump\.yml/);
    assert.match(workflowJob('deploy-production'), /needs: \[version, finalize-release\]/);
    assert.match(workflowJob('deploy-production'), /if: needs\.version\.outputs\.kind == 'release'/);
});

function workflowRun(job, stepName) {
    const step = job.split('      - name: ').find(part => part.startsWith(`${stepName}\n`));
    assert.ok(step, `Missing workflow step ${stepName}`);
    const run = step.split('        run: |\n')[1];
    assert.ok(run, `Missing shell script for ${stepName}`);
    return run.split('\n').map(line => line.slice(10)).join('\n');
}

test('both release publishers refresh stale-beta tags and fail closed on fetch errors', () => {
    for (const registry of ['nuget', 'npm']) {
        const job = workflowJob(`publish-${registry}-release`);
        assert.match(job, /if: needs\.version\.outputs\.kind == 'beta'/);
        assert.match(job, /if: steps\.stale-check\.outputs\.skip != 'true'/);
        const script = workflowRun(job, 'Skip stale beta if final tag now exists')
            .replaceAll('${{ needs.version.outputs.base }}', '0.42.0');
        assert.match(script, /set -euo pipefail/);
        assert.match(script, /git fetch --tags --quiet/);
        assert.doesNotMatch(script, /\|\| true/);
        for (const [fetchExit, tagExit, expected] of [[0, 0, 'true'], [0, 1, 'false'], [7, 0, null]]) {
            const run = () => execFileSync('bash', ['-c', `
              git() { if [ "$1" = fetch ]; then return ${fetchExit}; else return ${tagExit}; fi; }
              ${script}
            `], { env: { ...process.env, GITHUB_OUTPUT: '/dev/stdout' }, encoding: 'utf8', stdio: 'pipe' });
            if (expected === null) assert.throws(run);
            else assert.match(run(), new RegExp(`skip=${expected}`));
        }
    }
});

test('release lookup treats only HTTP 404 as missing and never hides network/auth/server errors', () => {
    const script = workflowRun(workflowJob('finalize-release'), 'Check if GitHub Release exists');
    for (const [http, exit, expected] of [
        ['200', 0, 'true'], ['404', 0, 'false'], ['401', 0, null], ['403', 0, null],
        ['429', 0, null], ['500', 0, null], ['000', 7, null],
    ]) {
        const run = () => execFileSync('bash', ['-c', `
          curl() { printf '%s' '${http}'; return ${exit}; }
          ${script}
        `], {
            env: {
                ...process.env, GITHUB_OUTPUT: '/dev/stdout', GH_TOKEN: 'unused-test-credential',
                PACKAGE_VERSION: '0.42.0', GITHUB_API_URL: 'https://api.github.com', GITHUB_REPOSITORY: 'mitchdenny/hex1b',
            },
            encoding: 'utf8', stdio: 'pipe',
        });
        if (expected === null) assert.throws(run);
        else assert.equal(run().trim(), `exists=${expected}`);
    }
});

test('NuGet lookup supports all-present retries and fails closed on non-404 errors', () => {
    const script = workflowRun(workflowJob('publish-nuget-release'), 'Check if packages exist on NuGet.org')
        .replaceAll('${{ needs.version.outputs.version }}', '0.42.0');
    for (const [http, exit, expected] of [['200', 0, 'true'], ['404', 0, 'false'], ['403', 0, null], ['503', 0, null], ['000', 7, null]]) {
        const run = () => execFileSync('bash', ['-c', `
          curl() { printf '%s' '${http}'; return ${exit}; }
          ${script}
        `], { env: { ...process.env, GITHUB_OUTPUT: '/dev/stdout' }, encoding: 'utf8', stdio: 'pipe' });
        if (expected === null) assert.throws(run);
        else assert.match(run(), new RegExp(`exists=${expected}`));
    }
});
