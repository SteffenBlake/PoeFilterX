import { execFileSync } from 'node:child_process';
import { appendFileSync, readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const stableTag = /^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$/;

export function releaseMetadata({ eventName, event, commit, runNumber, tags, fallbackVersion }) {
    if (!/^[a-f0-9]{40}$/.test(commit)) {
        throw new Error('Expected a full Git commit hash.');
    }

    if (eventName === 'release') {
        const release = event.release;
        const match = stableTag.exec(release?.tag_name ?? '');
        if (!match || release.draft || release.prerelease) {
            throw new Error('Stable releases require a published X.Y.Z or vX.Y.Z tag. Use the manual workflow for prereleases.');
        }

        const version = match.slice(1).join('.');
        return { commit, version, 'extension-version': version, prerelease: 'false', tag: release.tag_name };
    }

    if (eventName !== 'workflow_dispatch' || !/^[1-9]\d*$/.test(runNumber)) {
        throw new Error('Expected a manual workflow run with a valid run number.');
    }

    const latest = tags.filter(tag => stableTag.test(tag))
        .map(tag => tag.replace(/^v/, ''))
        .toSorted(new Intl.Collator('en', { numeric: true }).compare)
        .at(-1);
    const match = stableTag.exec(latest ?? fallbackVersion);
    if (!match) {
        throw new Error('Cannot determine a valid base version for the prerelease.');
    }

    const [major, minor, patch] = match.slice(1).map(Number);
    const version = `${major}.${minor}.${patch}-preview.${runNumber}`;
    const extensionVersion = `${major}.${minor}.${runNumber}000${patch}`;
    return {
        commit, version, 'extension-version': extensionVersion, prerelease: 'true',
        tag: `preview-${commit.slice(0, 12)}-${runNumber}`
    };
}

function main() {
    const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).trim();
    const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
    const commit = git('rev-parse', 'HEAD');
    if (process.env.GITHUB_EVENT_NAME === 'release') {
        const tagCommit = git('rev-parse', '--verify', `refs/tags/${event.release.tag_name}^{commit}`);
        if (tagCommit !== commit) {
            throw new Error('The checked-out commit does not match the release tag.');
        }

        execFileSync('git', ['merge-base', '--is-ancestor', commit, 'origin/main']);
    }

    const metadata = releaseMetadata({
        eventName: process.env.GITHUB_EVENT_NAME,
        event,
        commit,
        runNumber: process.env.GITHUB_RUN_NUMBER,
        tags: git('tag', '--merged', 'HEAD', '--sort=-version:refname').split('\n'),
        fallbackVersion: JSON.parse(readFileSync('editors/vscode/package.json', 'utf8')).version
    });
    appendFileSync(process.env.GITHUB_OUTPUT, Object.entries(metadata).map(([key, value]) => `${key}=${value}\n`).join(''));
    console.log(JSON.stringify(metadata));
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    main();
}
