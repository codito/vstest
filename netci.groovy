// Import the utility functionality.
import jobs.generation.Utilities;

// Defines a the new of the repo, used elsewhere in the file
def project = GithubProject
def branch = GithubBranchName

// Generate the builds for debug and release, commit and PRJob
[true, false].each { isPR -> // Defines a closure over true and false, value assigned to isPR
    ['Debug', 'Release'].each { configuration ->
        
        // Determine the name for the new job. A _prtest suffix is appended if isPR is true.
        def newJobName = Utilities.getFullJobName(project, configuration, isPR)
        
        // Define your build/test strings here
        def buildString = """call build.cmd -c ${configuration}"""
        def testString = """call test.cmd -c ${configuration}"""
        def platformtestString = """call test.cmd -c ${configuration} -p platformtests"""
        def smoketestString = """call test.cmd -c ${configuration} -p smoke"""
        def acceptancetestString = """call test.cmd -c ${configuration} -p AcceptanceTests"""

        // Create a new job for windows build
        def newJob = job(newJobName) {
            steps {
                batchFile(buildString)
                batchFile(testString)
                batchFile(platformtestString)
                batchFile(smoketestString)
                batchFile(acceptancetestString)
            }
        }

        Utilities.setMachineAffinity(newJob, 'Windows_NT', 'latest-or-auto')
        
        // This call performs remaining common job setup on the newly created job.
        Utilities.standardJobSetup(newJob, project, isPR, "*/${branch}")

        if (isPR) {
            Utilities.addGithubPRTriggerForBranch(newJob, branch, "Windows / ${configuration} Build")
        }
        else {
            Utilities.addGithubPushTrigger(newJob)
        }
    }
}
