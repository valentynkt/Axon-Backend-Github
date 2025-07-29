#!/bin/bash
# Auto-generates Docs/INDEX.md based on existing documentation files

set -euo pipefail

DOCS_PATH="${1:-Docs}"
INDEX_PATH="${DOCS_PATH}/INDEX.md"

if [[ ! -d "$DOCS_PATH" ]]; then
    echo "❌ Error: Docs directory not found: $DOCS_PATH"
    exit 1
fi

echo "📝 Updating documentation index..."

# Generate INDEX.md content
cat > "$INDEX_PATH" << 'EOF'
# Docs Index

## Root Documentation
EOF

# Add root .md files (excluding INDEX.md)
find "$DOCS_PATH" -maxdepth 1 -name "*.md" ! -name "INDEX.md" | sort | while read -r file; do
    filename=$(basename "$file")
    echo "- [$filename]($filename)" >> "$INDEX_PATH"
done

echo "" >> "$INDEX_PATH"

# Add ADR templates if they exist
if [[ -d "$DOCS_PATH/adr" ]] && [[ $(find "$DOCS_PATH/adr" -name "*.md" | wc -l) -gt 0 ]]; then
    echo "## ADR Templates" >> "$INDEX_PATH"
    find "$DOCS_PATH/adr" -name "*.md" | sort | while read -r file; do
        rel_path=${file#$DOCS_PATH/}
        echo "- [$rel_path]($rel_path)" >> "$INDEX_PATH"
    done
    echo "" >> "$INDEX_PATH"
fi

# Add API contracts if they exist
if [[ -d "$DOCS_PATH/contracts" ]] && [[ $(find "$DOCS_PATH/contracts" -name "*.md" | wc -l) -gt 0 ]]; then
    echo "## API Contracts" >> "$INDEX_PATH"
    find "$DOCS_PATH/contracts" -name "*.md" | sort | while read -r file; do
        rel_path=${file#$DOCS_PATH/}
        echo "- [$rel_path]($rel_path)" >> "$INDEX_PATH"
    done
    echo "" >> "$INDEX_PATH"
fi

# Add references if they exist
if [[ -d "$DOCS_PATH/references" ]] && [[ $(find "$DOCS_PATH/references" -name "*.md" | wc -l) -gt 0 ]]; then
    echo "## References" >> "$INDEX_PATH"
    find "$DOCS_PATH/references" -name "*.md" | sort | while read -r file; do
        rel_path=${file#$DOCS_PATH/}
        echo "- [$rel_path]($rel_path)" >> "$INDEX_PATH"
    done
    echo "" >> "$INDEX_PATH"
fi

# Add feature documentation if it exists
if [[ -d "$DOCS_PATH/features" ]] && [[ $(find "$DOCS_PATH/features" -name "*.md" | wc -l) -gt 0 ]]; then
    echo "## Feature Documentation" >> "$INDEX_PATH"
    echo "" >> "$INDEX_PATH"
    
    # Group by module/feature directories
    find "$DOCS_PATH/features" -mindepth 2 -maxdepth 2 -type d | sort | while read -r feature_dir; do
        feature_path=${feature_dir#$DOCS_PATH/features/}
        echo "### $feature_path" >> "$INDEX_PATH"
        
        find "$feature_dir" -name "*.md" | sort | while read -r file; do
            rel_path=${file#$DOCS_PATH/}
            echo "- [$rel_path]($rel_path)" >> "$INDEX_PATH"
        done
        echo "" >> "$INDEX_PATH"
    done
fi

echo "✅ Updated $INDEX_PATH"
echo "   Generated index with $(wc -l < "$INDEX_PATH") lines"