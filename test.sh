#!/usr/bin/env bash
# Copyright (c) Microsoft. All rights reserved.
# Build script for Test Platform.

set -o nounset  # Fail on uninitialized variables.
set -e          # Fail on non-zero exit code.

# ANSI color codes
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[0;33m'
NOCOLOR='\033[0m'

#
# Parse options
#
CONFIGURATION="Debug"
TARGET_RUNTIME="ubuntu.16.04-x64"
VERSION="15.0.0"
VERSION_SUFFIX="dev"
FAIL_FAST=false
DISABLE_LOCALIZED_BUILD=false
CI_BUILD=false
VERBOSE=false
PROJECT_NAME_PATTERNS=**Unit**csproj

while [ $# -gt 0 ]; do
    lowerI="$(echo ${1:-} | awk '{print tolower($0)}')"
    case $lowerI in
        -h | --help)
            usage
            exit
            ;;
        -c)
            CONFIGURATION=$2
            ;;
        -r)
            TARGET_RUNTIME=$2
            ;;
        -v)
            VERSION=$2
            ;;
        -vs)
            VERSION_SUFFIX=$2
            ;;
        -noloc)
            DISABLE_LOCALIZED_BUILD=true
            ;;
        -ci)
            CI_BUILD=$2
            ;;
        -p)
            PROJECT_NAME_PATTERNS=$2
            ;;
        -verbose)
            VERBOSE=$2
            ;;
        *)
            break
            ;;
   esac
   shift
done

#
# Variables
#
TP_ROOT_DIR=$(cd "$(dirname "$0")"; pwd -P)
TP_TOOLS_DIR="$TP_ROOT_DIR/tools"
TP_PACKAGES_DIR="$TP_ROOT_DIR/packages"
TP_OUT_DIR="$TP_ROOT_DIR/artifacts"
TP_PACKAGE_PROJ_DIR="$TP_ROOT_DIR/src/package/package"
TP_PACKAGE_NUSPEC_DIR="$TP_ROOT_DIR/src/package/nuspec"
TP_SRC_DIR="$TP_ROOT_DIR/src"

#
# Dotnet configuration
#
# Disable first run since we want to control all package sources 
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
# Dotnet build doesnt support --packages yet. See https://github.com/dotnet/cli/issues/2712
export NUGET_PACKAGES=$TP_PACKAGES_DIR
DOTNET_CLI_VERSION="latest"

#
# Build configuration
#
TPB_Solution="TestPlatform.sln"
TPB_TargetFrameworkCore="netcoreapp1.0"
TPB_Configuration=$CONFIGURATION
TPB_TargetRuntime=$TARGET_RUNTIME
TPB_Version=$VERSION
TPB_VersionSuffix=$VERSION_SUFFIX
TPB_CIBuild=$CI_BUILD
TPB_LocalizedBuild=$DISABLE_LOCALIZED_BUILD
TPB_Verbose=$VERBOSE

#
# Logging
#
log()
{
    printf "${GREEN}... $@${NOCOLOR}\n"
}

verbose()
{
    if [ ${TPB_Verbose-false} ]
    then
        printf "${YELLOW}... $@${NOCOLOR}\n" >&2
    fi
}

error()
{
    printf "${RED}... $@${NOCOLOR}\n" >&2
}

function usage()
{
    log " Usage: ./build.sh [Options]"
    log ""
    log " -c <CONFIGURATION>                Build the specified Configuration (Debug or Release, default: Debug)"
    log " -r <TARGET_RUNTIME>               Build for the specified runtime moniker (ubuntu.14.04-x64)"
    log " -v <VERSION>                      Version number for the package generated (15.0.0)"
    log " -vs <VERSION_SUFFIX>              Version suffix for package generated (dev)"
    log " -noloc <DISABLE_LOCALIZED_BUILD>  Disable Localized builds (true,false)"
    log " -ci <CI_BUILD>                    Declares if this is a CI_BUILD or not"
    log " -p <PROJECT_NAME_PATTERNS>        Pattern to build specific projects"
    log " -verbose <VERBOSE>                Enable verbose logging (true, false)"
}

#
# Test steps
#
function invoke_test()
{
    local dotnet=$(_get_dotnet_path)

    #if [ ${TPB_Watch-false} ]
    #then
        #watchman-make -p "**(src|test)/*" -r "find ./test -name $PROJECT_NAME_PATTERNS | xargs -L 1 $dotnet test -f netcoreapp1.0"
    #fi

    find ./test -path $PROJECT_NAME_PATTERNS | xargs $dotnet vstest --parallel
}

#
# Privates
#
_get_dotnet_path()
{
    echo "$TP_TOOLS_DIR/dotnet/dotnet"
}

invoke_test
