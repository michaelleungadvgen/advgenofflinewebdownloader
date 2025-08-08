# 🚀 CI/CD Workflows for AdvGen Offline Web Downloader

This directory contains GitHub Actions workflows for automated building, testing, security scanning, and releasing of the WinUI application.

## 📋 Available Workflows

### 1. Build Workflow (`build.yml`)
**Triggers:** Push to main/dev branches, Pull requests
**Purpose:** Basic build validation and testing

- ✅ Builds the application in Debug and Release configurations
- ✅ Verifies build output integrity
- ✅ Runs tests if available
- ✅ Uploads build artifacts for Release builds
- ✅ Provides detailed build summaries

### 2. CI/CD Pipeline (`ci-cd.yml`)
**Triggers:** Push to main/dev, Pull requests, Releases
**Purpose:** Comprehensive build, test, security, and deployment pipeline

- 🏗️ **Multi-platform builds** (x64, x86, ARM64)
- 🧪 **Automated testing** with result uploads
- 📦 **Package creation** for releases
- 🛡️ **Security scanning** with CodeQL and dependency checks
- 📊 **Code quality analysis** with SonarCloud support
- 🚀 **Automated release asset uploads**
- 📧 **Deployment notifications**

### 3. Release Workflow (`release.yml`)
**Triggers:** GitHub releases, Manual dispatch
**Purpose:** Create and distribute release packages

- 📦 **Multi-platform packaging** (x64, x86, ARM64)
- 📝 **Installation documentation** generation
- 🧪 **Package integrity testing**
- 📤 **Automatic release asset uploads**
- 🎯 **Portable deployment packages**

### 4. Security Workflow (`security.yml`)
**Triggers:** Schedule (weekly), Dependency changes, Manual dispatch
**Purpose:** Security and compliance monitoring

- 🔍 **OWASP dependency vulnerability scanning**
- 🛡️ **CodeQL security analysis**
- 📄 **License compliance checking**
- 📊 **Security reporting and summaries**
- ⚠️ **Automated vulnerability alerts**

## 🔧 Setup Instructions

### 1. Repository Configuration
Ensure your repository has the following structure:
```
your-repo/
├── .github/
│   └── workflows/
│       ├── build.yml
│       ├── ci-cd.yml
│       ├── release.yml
│       └── security.yml
├── advgenofflinewebdownloader/
│   ├── advgenofflinewebdownloader.csproj
│   └── [source files]
└── advgenofflinewebdownloader.sln
```

### 2. Required Secrets (Optional)
Add these secrets in your repository settings if needed:

- `SONAR_TOKEN`: SonarCloud token for code quality analysis
- Custom deployment tokens if using external services

### 3. Branch Protection (Recommended)
Configure branch protection rules for `main`:
- ✅ Require status checks to pass before merging
- ✅ Require branches to be up to date before merging
- ✅ Include administrators in restrictions

## 🎯 Usage Guide

### For Developers

1. **Development Workflow**
   ```bash
   git checkout -b feature/your-feature
   # Make your changes
   git push origin feature/your-feature
   # Create pull request → triggers build.yml
   ```

2. **Creating a Release**
   ```bash
   git tag v1.0.0
   git push origin v1.0.0
   # Create GitHub release → triggers release.yml
   ```

### For Maintainers

1. **Manual Release**
   - Go to Actions → Release workflow
   - Click "Run workflow"
   - Enter version and options

2. **Security Review**
   - Weekly automated scans via security.yml
   - Check Security tab for findings
   - Review dependency alerts

## 📊 Workflow Outputs

### Build Artifacts
- **Debug builds**: Retained for 7 days
- **Release builds**: Retained for 30 days
- **Release packages**: Retained for 90 days

### Reports
- **Test results**: TRX format with detailed results
- **Security reports**: HTML, JSON, and SARIF formats
- **License reports**: Dependency license compliance

### Release Assets
- **x64 Package**: `AdvGenOfflineWebDownloader-vX.X.X-x64.zip`
- **x86 Package**: `AdvGenOfflineWebDownloader-vX.X.X-x86.zip`
- **ARM64 Package**: `AdvGenOfflineWebDownloader-vX.X.X-ARM64.zip`

Each package includes:
- ✅ Application executable
- ✅ All required dependencies
- ✅ Installation instructions
- ✅ Launch script
- ✅ License and documentation

## 🔍 Monitoring and Maintenance

### Status Badges
Add these to your README.md:

```markdown
[![Build Status](https://github.com/your-username/advgenofflinewebdownloader/workflows/Build%20-%20WinUI%20App/badge.svg)](https://github.com/your-username/advgenofflinewebdownloader/actions/workflows/build.yml)
[![Security](https://github.com/your-username/advgenofflinewebdownloader/workflows/Security%20%26%20Dependencies/badge.svg)](https://github.com/your-username/advgenofflinewebdownloader/actions/workflows/security.yml)
[![Release](https://github.com/your-username/advgenofflinewebdownloader/workflows/Release%20-%20Create%20Distribution%20Packages/badge.svg)](https://github.com/your-username/advgenofflinewebdownloader/actions/workflows/release.yml)
```

### Regular Maintenance
- 📅 **Weekly**: Review security scan results
- 📅 **Monthly**: Update dependencies and review vulnerability reports
- 📅 **Quarterly**: Review and update workflow configurations

## 🛠️ Customization

### Modifying Build Configuration
Edit workflow files to customize:
- Target frameworks and platforms
- Build configurations
- Test execution parameters
- Package contents

### Adding New Platforms
To support additional architectures:
1. Add to `strategy.matrix.platform` in relevant workflows
2. Ensure publish profiles exist in `Properties/PublishProfiles/`
3. Update package naming and documentation

### Environment-Specific Builds
Create environment-specific workflows by:
1. Copying existing workflow files
2. Modifying trigger conditions
3. Adjusting environment variables
4. Customizing deployment targets

## 📞 Support

If you encounter issues with the CI/CD workflows:

1. **Check workflow logs** in the Actions tab
2. **Review build artifacts** for detailed error information
3. **Verify prerequisites** are met in runner environments
4. **Consult GitHub Actions documentation** for platform-specific issues

## 📈 Performance Optimization

### Build Speed
- ✅ Efficient dependency caching
- ✅ Parallel matrix builds
- ✅ Minimal verbosity for faster logs
- ✅ Conditional job execution

### Resource Usage
- ✅ Appropriate artifact retention periods
- ✅ Selective workflow triggers
- ✅ Optimized Docker layer usage
- ✅ Concurrent job limitations

---
*Last updated: $(date -u +"%Y-%m-%d")*