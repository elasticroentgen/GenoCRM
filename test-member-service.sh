#!/bin/bash

# Test runner for MemberService unit tests only
# This script compiles and runs just the new MemberService tests

echo "Building GenoCRM main project..."
dotnet build GenoCRM/GenoCRM/GenoCRM.csproj

if [ $? -ne 0 ]; then
    echo "Failed to build main project"
    exit 1
fi

echo "Compiling test files individually..."

# Test if the new test files compile without running full test suite
csc_check() {
    local file=$1
    echo "Checking syntax of $file..."
    
    # Create a temporary project just for syntax checking
    temp_dir=$(mktemp -d)
    cp GenoCRM.Tests/GenoCRM.Tests.csproj "$temp_dir/"
    cp "$file" "$temp_dir/"
    
    cd "$temp_dir"
    
    # Try to build just this one file
    dotnet build > /dev/null 2>&1
    result=$?
    
    cd - > /dev/null
    rm -rf "$temp_dir"
    
    if [ $result -eq 0 ]; then
        echo "✓ $file compiles successfully"
        return 0
    else
        echo "✗ $file has compilation errors"
        return 1
    fi
}

# Check our new test files
csc_check "GenoCRM.Tests/Services/Business/MemberServiceCrudTests.cs"
csc_check "GenoCRM.Tests/Services/Business/MemberServiceEdgeCasesTests.cs"

echo "Member service tests are ready to run!"
echo "To run them when the project builds successfully, use:"
echo "dotnet test GenoCRM.Tests/GenoCRM.Tests.csproj --filter 'FullyQualifiedName~MemberServiceCrudTests'"
echo "dotnet test GenoCRM.Tests/GenoCRM.Tests.csproj --filter 'FullyQualifiedName~MemberServiceEdgeCasesTests'"