"""Validate extracted counter actions or the widget design configuration."""
import json
import sys
from pathlib import Path


def validate(bundle, widget=False):
    metadata = Path(bundle) / "Metadata.appintents/extract.actionsdata"
    data = json.loads(metadata.read_bytes())
    text = json.dumps(data)
    required_names = ["StudioDesignIntent", "StudioDesignEntity"] if widget else ["IncrementControlStudioCounter", "Increment Control Studio Counter"]
    for required in required_names:
        if required not in text:
            raise ValueError(f"Missing App Intents metadata: {required}")
    # The exact metadata format is private and may change; retain it for review.
    # This establishes extraction/target membership, not device discovery or execution.
    print("Widget metadata contains the design configuration and entity." if widget else
          "App Intent metadata contains the expected action identifier and title.")


if __name__ == "__main__":
    validate(sys.argv[1], widget="--widget" in sys.argv[2:])
