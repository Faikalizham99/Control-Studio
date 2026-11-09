"""Validate the extracted Apple Shortcuts counter action."""
import argparse
import json
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


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("bundle", type=Path, help="Path to the extracted extension bundle")
    args = parser.parse_args(argv)
    try:
        validate(args.bundle)
    except (OSError, ValueError, RecursionError) as error:
        parser.exit(1, f"Metadata validation failed: {error}\n")


if __name__ == "__main__":
    main()
