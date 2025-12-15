#!/bin/bash
set -e

# Script to delete a specific PR environment resource group in Azure
# Usage: delete-pr-environment.sh <azure-subscription-id> <pr-number>

AZURE_SUBSCRIPTION_ID="${1}"
PR_NUMBER="${2}"

if [ -z "$AZURE_SUBSCRIPTION_ID" ]; then
    echo "Error: Azure subscription ID is required as first argument"
    echo "Usage: $0 <azure-subscription-id> <pr-number>"
    exit 1
fi

if [ -z "$PR_NUMBER" ]; then
    echo "Error: PR number is required as second argument"
    echo "Usage: $0 <azure-subscription-id> <pr-number>"
    exit 1
fi

# Validate PR number is numeric
if ! [[ "$PR_NUMBER" =~ ^[0-9]+$ ]]; then
    echo "Error: PR number must be numeric"
    exit 1
fi

echo "Deleting PR environment for PR #$PR_NUMBER..."
echo "Subscription: $AZURE_SUBSCRIPTION_ID"
echo ""

# Set the Azure subscription
az account set --subscription "$AZURE_SUBSCRIPTION_ID"

# Construct resource group name
RESOURCE_GROUP_NAME="hex1b-pr-${PR_NUMBER}"

echo "Checking if resource group exists: $RESOURCE_GROUP_NAME"

# Check if the resource group exists
if az group show --name "$RESOURCE_GROUP_NAME" &>/dev/null; then
    echo "✓ Resource group found: $RESOURCE_GROUP_NAME"
    echo "Deleting resource group..."
    
    # Delete the resource group
    if az group delete --name "$RESOURCE_GROUP_NAME" --yes --no-wait; then
        echo "✓ Deletion initiated successfully (running asynchronously)"
        echo "The resource group $RESOURCE_GROUP_NAME is being deleted."
    else
        echo "✗ Failed to delete resource group: $RESOURCE_GROUP_NAME"
        exit 1
    fi
else
    echo "ℹ Resource group not found: $RESOURCE_GROUP_NAME"
    echo "The environment may have already been deleted or never existed."
    exit 0
fi

echo ""
echo "Delete operation completed."
