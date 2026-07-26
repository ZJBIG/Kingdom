from setuptools import find_namespace_packages, setup


setup(
    name="cli-anything-kingdom",
    version="0.1.0",
    description="Agent-native CLI harness for the Kingdom Unity project",
    packages=find_namespace_packages(include=["cli_anything.*"]),
    entry_points={
        "console_scripts": [
            "cli-anything-kingdom=cli_anything.kingdom.kingdom_cli:main",
        ]
    },
    python_requires=">=3.10",
)
