module.exports = [
    {
        text: "Server Quick Start",
        children: [
            "README.md",
            "whatsnew.md",
            "installation.md",
            "usage-telemetry.md"
        ]
    },
    {
        text: "Configuration",
        children: [
            "configuration.md",
            "db-config.md",
            "security.md",
            "networking.md",
            "cluster.md",
        ]
    },
    {
        text: "Features",
        children: [
            "admin-ui.md",
            "streams.md",
            "indexes.md",
            "projections.md",
            "persistent-subscriptions.md",
        ]
    },
    {
        text: "Operations",
        children: [
            "upgrade-guide.md",
            "operations.md",
        ]
    },
    {
        text: "Diagnostics",
        group: "Diagnostics",
        prefix: "/diagnostics/",
        link: "/diagnostics/",
        children: [
            "README.md",
            "logs.md",
            "metrics.md",
            "integrations.md",
        ]
    }
];
