#!/bin/bash
set -e

# Script to cleanup stale PR environment resource groups in Azure
# Usage: cleanup-pr-environments.sh <azure-subscription-id> <github-repo> <github-token> [inactivity-threshold-hours]

AZURE_SUBSCRIPTION_ID="${1}"
GITHUB_REPO="${2}"
GITHUB_TOKEN="${3:-${GITHUB_TOKEN}}"
INACTIVITY_THRESHOLD_HOURS="${4:-1}"

if [ -z "$AZURE_SUBSCRIPTION_ID" ]; then
    echo "Error: Azure subscription ID is required as first argument"
    echo "Usage: $0 <azure-subscription-id> <github-repo> [github-token] [inactivity-threshold-hours]"
    exit 1
fi

if [ -z "$GITHUB_REPO" ]; then
    echo "Error: GitHub repository is required as second argument"
    echo "Usage: $0 <azure-subscription-id> <github-repo> [github-token] [inactivity-threshold-hours]"
    exit 1
fi

if [ -z "$GITHUB_TOKEN" ]; then
    echo "Error: GITHUB_TOKEN environment variable must be set or provided as third argument"
    exit 1
fi

echo "Starting PR environment cleanup..."
echo "Subscription: $AZURE_SUBSCRIPTION_ID"
echo "Repository: $GITHUB_REPO"
echo "Inactivity threshold: $INACTIVITY_THRESHOLD_HOURS hour(s)"
echo ""

# Set the Azure subscription
az account set --subscription "$AZURE_SUBSCRIPTION_ID"

# Calculate the threshold timestamp (current time - inactivity threshold)
THRESHOLD_TIMESTAMP=$(date -u -d "$INACTIVITY_THRESHOLD_HOURS hours ago" +%s)

# Get all resource groups matching the pattern hex1b-pr-*
echo "Fetching resource groups matching pattern 'hex1b-pr-*'..."
RESOURCE_GROUPS=$(az group list --query "[?starts_with(name, 'hex1b-pr-')].name" -o tsv)

if [ -z "$RESOURCE_GROUPS" ]; then
    echo "No PR resource groups found."
    exit 0
fi

echo "Found $(echo "$RESOURCE_GROUPS" | wc -l) resource group(s) to check"
echo ""

# Process each resource group
for RG_NAME in $RESOURCE_GROUPS; do
    echo "Checking resource group: $RG_NAME"
    
    # Extract PR number from resource group name (hex1b-pr-123 -> 123)
    if [[ $RG_NAME =~ hex1b-pr-([0-9]+) ]]; then
        PR_NUMBER="${BASH_REMATCH[1]}"
        echo "  PR Number: $PR_NUMBER"
        
        # Get PR details from GitHub API
        HTTP_CODE=$(curl -s -w "%{http_code}" -o "/tmp/pr_data_${PR_NUMBER}.json" \
            -H "Authorization: token $GITHUB_TOKEN" \
            -H "Accept: application/vnd.github.v3+json" \
            "https://api.github.com/repos/$GITHUB_REPO/pulls/$PR_NUMBER")
        
        if [ "$HTTP_CODE" -ne 200 ]; then
            echo "  GitHub API returned HTTP $HTTP_CODE. Skipping deletion for safety."
            rm -f "/tmp/pr_data_${PR_NUMBER}.json"
            echo ""
            continue
        fi
        
        PR_DATA=$(cat "/tmp/pr_data_${PR_NUMBER}.json")
        rm -f "/tmp/pr_data_${PR_NUMBER}.json"
        
        # Check if PR exists
        PR_STATE=$(echo "$PR_DATA" | jq -r '.state // "not_found"')
        
        if [ "$PR_STATE" == "not_found" ] || [ "$PR_STATE" == "null" ]; then
            echo "  PR #$PR_NUMBER not found or API error. Skipping deletion for safety."
            echo ""
            continue
        fi
        
        echo "  PR State: $PR_STATE"
        
        # Get the last updated timestamp
        UPDATED_AT=$(echo "$PR_DATA" | jq -r '.updated_at')
        
        if [ -z "$UPDATED_AT" ] || [ "$UPDATED_AT" == "null" ]; then
            echo "  Could not determine last update time. Skipping."
            echo ""
            continue
        fi
        
        # Convert GitHub timestamp to Unix timestamp
        UPDATED_TIMESTAMP=$(date -u -d "$UPDATED_AT" +%s)
        
        # Calculate time difference in seconds (using UTC for both)
        TIME_DIFF=$(($(date -u +%s) - UPDATED_TIMESTAMP))
        TIME_DIFF_HOURS=$((TIME_DIFF / 3600))
        
        echo "  Last updated: $UPDATED_AT (${TIME_DIFF_HOURS} hours ago)"
        
        # Check if PR has been inactive for more than the threshold
        if [ "$UPDATED_TIMESTAMP" -lt "$THRESHOLD_TIMESTAMP" ]; then
            echo "  ✓ PR has been inactive for more than $INACTIVITY_THRESHOLD_HOURS hour(s)"
            echo "  Deleting resource group: $RG_NAME"
            
            # Delete the resource group
            if az group delete --name "$RG_NAME" --yes --no-wait; then
                echo "  ✓ Deletion initiated (running asynchronously)"
                
                # Post comment to PR about deletion
                cat > /tmp/pr_cleanup_comment.md << 'EOF'
## 🗑️ Environment Cleanup

The PR environment `RG_NAME_PLACEHOLDER` has been automatically deleted due to inactivity (PR was inactive for more than THRESHOLD_PLACEHOLDER hour(s)).

This operation runs asynchronously and may take a few minutes to complete.

---
*Cleanup initiated at TIMESTAMP_PLACEHOLDER*
EOF

                sed -i "s|RG_NAME_PLACEHOLDER|${RG_NAME}|g" /tmp/pr_cleanup_comment.md
                sed -i "s|THRESHOLD_PLACEHOLDER|${INACTIVITY_THRESHOLD_HOURS}|g" /tmp/pr_cleanup_comment.md
                sed -i "s|TIMESTAMP_PLACEHOLDER|$(date -u '+%Y-%m-%d %H:%M:%S UTC')|g" /tmp/pr_cleanup_comment.md
                
                # Use GitHub CLI to post comment
                if command -v gh &> /dev/null; then
                    gh pr comment "$PR_NUMBER" \
                        --body-file /tmp/pr_cleanup_comment.md \
                        --repo "$GITHUB_REPO"
                    echo "  ✓ Posted cleanup notification to PR #$PR_NUMBER"
                else
                    # Fallback to curl if gh CLI is not available
                    RESPONSE=$(curl -s -w "\n%{http_code}" -X POST \
                        -H "Authorization: token $GITHUB_TOKEN" \
                        -H "Accept: application/vnd.github.v3+json" \
                        "https://api.github.com/repos/$GITHUB_REPO/issues/$PR_NUMBER/comments" \
                        -d "{\"body\": $(cat /tmp/pr_cleanup_comment.md | jq -Rs .)}")
                    HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
                    if [ "$HTTP_CODE" -eq 201 ]; then
                        echo "  ✓ Posted cleanup notification to PR #$PR_NUMBER (using curl fallback)"
                    else
                        echo "  ⚠ Warning: Failed to post comment (HTTP $HTTP_CODE)"
                    fi
                fi
                
                rm -f /tmp/pr_cleanup_comment.md
            else
                echo "  ✗ Failed to delete resource group: $RG_NAME"
            fi
        else
            echo "  ✗ PR is still active (updated within last $INACTIVITY_THRESHOLD_HOURS hour(s)). Keeping resource group."
        fi
    else
        echo "  Warning: Could not extract PR number from resource group name: $RG_NAME"
    fi
    
    echo ""
done

echo "Cleanup process completed."
