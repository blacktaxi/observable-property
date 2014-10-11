#!/bin/bash

if [ ! -f .paket/paket.exe ]; then
    mono .paket/paket.bootstrapper.exe prerelease
    exit_code=$?
    if [ $exit_code -ne 0 ]; then
    	exit $exit_code
    fi
fi

mono .paket/paket.exe install -v
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx
