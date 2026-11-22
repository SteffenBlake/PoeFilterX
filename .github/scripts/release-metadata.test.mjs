import assert from 'node:assert/strict';
import { test } from 'node:test';
import { releaseMetadata } from './release-metadata.mjs';

const commit = '4147279acc625a1bb19fe91b1a4440e24d9e82d3';
const manual = {
    eventName: 'workflow_dispatch', event: {}, commit,
    runNumber: '42', tags: ['preview-old', 'v1.0.0', '3.28.0', 'v3.30.0', '3.9.0'], fallbackVersion: '0.1.3'
};

test('stable packages use the exact numeric tag version, with or without v', () => {
    for (const tag of ['3.31.0', 'v3.31.0']) {
        assert.deepEqual(releaseMetadata({ ...manual, eventName: 'release', event: { release: { tag_name: tag } } }), {
            commit, version: '3.31.0', 'extension-version': '3.31.0', prerelease: 'false', tag
        });
    }
});

test('release events reject unsupported versions and GitHub prereleases', () => {
    for (const release of [
        { tag_name: 'latest' }, { tag_name: 'v01.2.3' }, { tag_name: 'v3.31.0-preview.1' },
        { tag_name: 'v3.31.0', prerelease: true }, { tag_name: 'v3.31.0', draft: true }
    ]) {
        assert.throws(() => releaseMetadata({ ...manual, eventName: 'release', event: { release } }));
    }
});

test('manual versions preserve the latest stable base and include the run number', () => {
    const result = releaseMetadata(manual);
    assert.equal(result.version, '3.30.0-preview.42');
    assert.equal(result.prerelease, 'true');
    assert.equal(result.tag, 'preview-4147279acc62-42');
    assert.equal(result['extension-version'], '3.30.420000');
    assert.deepEqual(releaseMetadata(manual), result);
    assert.notEqual(result.version, releaseMetadata({ ...manual, runNumber: '43' }).version);
    assert.notEqual(result.tag, releaseMetadata({ ...manual, commit: 'a'.repeat(40) }).tag);
});

test('extension prereleases concatenate the run, three zeroes, and the stable patch', () => {
    for (const patch of [0, 1, 12]) {
        const result = releaseMetadata({ ...manual, tags: [`v3.30.${patch}`] });
        assert.equal(result.version, `3.30.${patch}-preview.42`);
        assert.equal(result['extension-version'], `3.30.42000${patch}`);
    }
    assert.equal(releaseMetadata({ ...manual, runNumber: '43' })['extension-version'], '3.30.430000');
});

test('a repository without stable tags uses the manifest version as its base', () => {
    const result = releaseMetadata({ ...manual, tags: [] });
    assert.equal(result.version, '0.1.3-preview.42');
    assert.equal(result['extension-version'], '0.1.420003');
});

test('invalid events, commit hashes and run identifiers fail before publishing', () => {
    for (const invalid of [
        { eventName: 'push' }, { commit: 'main' }, { runNumber: '0' }, { runNumber: '1\nversion=bad' },
        { tags: [], fallbackVersion: 'invalid' }
    ]) {
        assert.throws(() => releaseMetadata({ ...manual, ...invalid }));
    }
});
