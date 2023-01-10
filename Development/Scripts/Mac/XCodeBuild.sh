#!/bin/sh
# Copyright (c) 2012-2023 Wojciech Figat. All rights reserved

# Fix mono bin to be in a path
export PATH=/Library/Frameworks/Mono.framework/Versions/Current/Commands:$PATH

echo "Running Flax.Build $*"
mono Binaries/Tools/Flax.Build.exe "$@"
