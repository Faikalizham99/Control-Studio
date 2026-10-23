"""Validate the extracted Apple Shortcuts counter action."""
import json
import sys
from pathlib import Path


def validate(bundle):
    metadata = Path(bundle) / "Metadata.appintents/extract.actionsdata"
    data = json.loads(metadata.read_bytes())
    text = json.dumps(data)
    required_names = ["IncrementControlStudioCounter", "Increment Control Studio Counter"]
    for required in required_names:
        if required not in text:
            raise ValueError(f"Missing App Intents metadata: {required}")
    # The exact metadata format is private and may change; retain it for review.
    # This establishes extraction/target membership, not device discovery or execution.
    print("App Intent metadata contains the expected action identifier and title.")


if __name__ == "__main__":
    validate(sys.argv[1])
